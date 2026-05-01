using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerOpportunityEvaluator
{
    private readonly PlannerAutonomyPolicy autonomyPolicy;
    private readonly OpportunityTaskPipeline taskPipeline;
    private readonly IOpportunityRepository opportunityRepository;

    public PlannerOpportunityEvaluator(
        PlannerAutonomyPolicy autonomyPolicy,
        OpportunityTaskPipeline taskPipeline,
        IOpportunityRepository opportunityRepository)
    {
        this.autonomyPolicy = autonomyPolicy;
        this.taskPipeline = taskPipeline;
        this.opportunityRepository = opportunityRepository;
    }

    public PlannerOpportunityEvaluationResult Evaluate(RuntimeSessionState session, OpportunitySnapshot snapshot)
    {
        var preview = Preview(session, snapshot);
        if (!preview.ProducedWork)
        {
            return new PlannerOpportunityEvaluationResult(
                preview.Reason,
                preview.PlannerRound,
                preview.DetectedOpportunityCount,
                preview.EvaluatedOpportunityCount,
                preview.OpportunitySourceSummary,
                Array.Empty<string>(),
                preview.AutonomyLimit,
                preview.RequiresOperatorPause);
        }

        var materialization = taskPipeline.Materialize(preview.SelectedOpportunities, preview.PlannerRound);
        var refreshed = opportunityRepository.Load();
        var now = DateTimeOffset.UtcNow;
        foreach (var opportunity in refreshed.Items.Where(item => preview.SelectedOpportunities.Any(selected => string.Equals(selected.OpportunityId, item.OpportunityId, StringComparison.Ordinal))))
        {
            if (materialization.MaterializedTaskIdsByOpportunity.TryGetValue(opportunity.OpportunityId, out var taskIds))
            {
                opportunity.MarkMaterialized(taskIds, $"materialized during planner round {preview.PlannerRound}", now);
            }
        }

        opportunityRepository.Save(new OpportunitySnapshot
        {
            Version = refreshed.Version,
            GeneratedAt = now,
            Items = refreshed.Items,
        });
        return new PlannerOpportunityEvaluationResult(
            $"Planner evaluated {preview.EvaluatedOpportunityCount} open opportunities and proposed {materialization.MaterializedTaskIds.Count} governed tasks.",
            preview.PlannerRound,
            preview.DetectedOpportunityCount,
            preview.EvaluatedOpportunityCount,
            preview.OpportunitySourceSummary,
            materialization.MaterializedTaskIds,
            preview.AutonomyLimit,
            true);
    }

    public PlannerOpportunityPreviewResult Preview(RuntimeSessionState session, OpportunitySnapshot snapshot)
    {
        var plannerRound = session.PlannerRound + 1;
        var openOpportunities = snapshot.Items
            .Where(item => item.Status == OpportunityStatus.Open)
            .OrderBy(item => SeverityWeight(item.Severity))
            .ThenByDescending(item => item.Confidence)
            .ThenBy(item => item.OpportunityId, StringComparer.Ordinal)
            .ToArray();

        if (plannerRound > autonomyPolicy.MaxPlannerRounds)
        {
            MarkEvaluation(openOpportunities, "planner round cap reached");
            opportunityRepository.Save(RefreshSnapshot(snapshot));
            return new PlannerOpportunityPreviewResult(
                $"Planner autonomy blocked re-entry at round {plannerRound}; configured cap is {autonomyPolicy.MaxPlannerRounds}.",
                plannerRound,
                snapshot.Items.Count,
                openOpportunities.Length,
                SummarizeSources(openOpportunities),
                Array.Empty<Opportunity>(),
                EmptyPreview(),
                PlannerAutonomyLimit.PlannerRoundCap,
                true);
        }

        if (openOpportunities.Length == 0)
        {
            opportunityRepository.Save(RefreshSnapshot(snapshot));
            return new PlannerOpportunityPreviewResult(
                "No open opportunities were available for planner evaluation.",
                plannerRound,
                snapshot.Items.Count,
                0,
                "(none)",
                Array.Empty<Opportunity>(),
                EmptyPreview(),
                PlannerAutonomyLimit.None,
                false);
        }

        var selected = new List<Opportunity>();
        var generatedTaskBudget = 0;
        var autonomyLimit = PlannerAutonomyLimit.None;
        var blockReason = string.Empty;

        foreach (var opportunity in openOpportunities)
        {
            if (selected.Count >= autonomyPolicy.MaxOpportunitiesPerRound)
            {
                autonomyLimit = PlannerAutonomyLimit.OpportunityRoundCap;
                blockReason = $"Planner autonomy limited this round to {autonomyPolicy.MaxOpportunitiesPerRound} opportunities.";
                break;
            }

            if (opportunity.Source == OpportunitySource.Refactoring && opportunity.RelatedFiles.Count > autonomyPolicy.MaxRefactorScopeFiles)
            {
                opportunity.LastEvaluationReason = $"refactor scope exceeds planner autonomy cap ({opportunity.RelatedFiles.Count}/{autonomyPolicy.MaxRefactorScopeFiles} files)";
                continue;
            }

            const int projectedTasksPerOpportunity = 4;
            if (generatedTaskBudget + projectedTasksPerOpportunity > autonomyPolicy.MaxGeneratedTasks)
            {
                autonomyLimit = PlannerAutonomyLimit.GeneratedTaskCap;
                blockReason = $"Planner autonomy would exceed generated-task cap ({autonomyPolicy.MaxGeneratedTasks}).";
                break;
            }

            selected.Add(opportunity);
            generatedTaskBudget += projectedTasksPerOpportunity;
        }

        if (selected.Count == 0)
        {
            if (autonomyLimit == PlannerAutonomyLimit.None)
            {
                autonomyLimit = PlannerAutonomyLimit.RefactorScopeCap;
                blockReason = "Open opportunities exceeded refactor scope caps or were otherwise not actionable.";
            }

            MarkEvaluation(openOpportunities, blockReason);
            opportunityRepository.Save(RefreshSnapshot(snapshot));
            return new PlannerOpportunityPreviewResult(
                blockReason,
                plannerRound,
                snapshot.Items.Count,
                openOpportunities.Length,
                SummarizeSources(openOpportunities),
                Array.Empty<Opportunity>(),
                EmptyPreview(),
                autonomyLimit,
                true);
        }

        foreach (var opportunity in openOpportunities.Except(selected))
        {
            opportunity.LastEvaluationReason ??= "deferred for later planner round";
        }

        opportunityRepository.Save(RefreshSnapshot(snapshot));
        var preview = taskPipeline.Preview(selected, plannerRound);
        return new PlannerOpportunityPreviewResult(
            $"Planner evaluated {openOpportunities.Length} open opportunities and prepared {preview.ProposedTasks.Count} governed tasks for proposal review.",
            plannerRound,
            snapshot.Items.Count,
            openOpportunities.Length,
            SummarizeSources(selected),
            selected,
            preview,
            PlannerAutonomyLimit.None,
            true);
    }

    private static OpportunityTaskPreviewResult EmptyPreview()
    {
        return new OpportunityTaskPreviewResult(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            Array.Empty<Carves.Runtime.Domain.Tasks.TaskNode>());
    }

    private static void MarkEvaluation(IEnumerable<Opportunity> opportunities, string reason)
    {
        foreach (var opportunity in opportunities)
        {
            opportunity.LastEvaluationReason = reason;
        }
    }

    private static OpportunitySnapshot RefreshSnapshot(OpportunitySnapshot snapshot)
    {
        return new OpportunitySnapshot
        {
            Version = snapshot.Version,
            GeneratedAt = DateTimeOffset.UtcNow,
            Items = snapshot.Items
                .OrderBy(item => StatusWeight(item.Status))
                .ThenBy(item => SeverityWeight(item.Severity))
                .ThenBy(item => item.OpportunityId, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static string SummarizeSources(IEnumerable<Opportunity> opportunities)
    {
        var sources = opportunities
            .Select(opportunity => opportunity.Source.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return sources.Length == 0 ? "(none)" : string.Join(", ", sources);
    }

    private static int StatusWeight(OpportunityStatus status)
    {
        return status switch
        {
            OpportunityStatus.Open => 0,
            OpportunityStatus.Materialized => 1,
            OpportunityStatus.Resolved => 2,
            OpportunityStatus.Dismissed => 3,
            _ => 99,
        };
    }

    private static int SeverityWeight(OpportunitySeverity severity)
    {
        return severity switch
        {
            OpportunitySeverity.Critical => 0,
            OpportunitySeverity.High => 1,
            OpportunitySeverity.Medium => 2,
            OpportunitySeverity.Low => 3,
            _ => 99,
        };
    }
}
