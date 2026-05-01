using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Planning;

public sealed class OpportunityTaskPipeline
{
    private static readonly TaskTypePolicy TaskTypePolicy = TaskTypePolicy.Default;
    private readonly string repoRoot;
    private readonly TaskGraphService taskGraphService;
    private readonly TaskDecomposer taskDecomposer;
    private readonly IGitClient gitClient;
    private readonly SystemConfig systemConfig;
    private readonly ICodeGraphQueryService? codeGraphQueryService;

    public OpportunityTaskPipeline(
        string repoRoot,
        TaskGraphService taskGraphService,
        TaskDecomposer taskDecomposer,
        IGitClient gitClient,
        SystemConfig systemConfig,
        ICodeGraphQueryService? codeGraphQueryService = null)
    {
        this.repoRoot = repoRoot;
        this.taskGraphService = taskGraphService;
        this.taskDecomposer = taskDecomposer;
        this.gitClient = gitClient;
        this.systemConfig = systemConfig;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public OpportunityTaskMaterializationResult Materialize(IReadOnlyList<Opportunity> opportunities, int plannerRound)
    {
        var graph = taskGraphService.Load();
        var preview = Preview(opportunities, plannerRound);
        var additions = new List<TaskNode>();
        foreach (var task in preview.ProposedTasks)
        {
            AddIfMissing(graph, additions, task);
        }

        if (additions.Count > 0)
        {
            taskGraphService.AddTasks(additions);
        }

        return new OpportunityTaskMaterializationResult(preview.ProposedTaskIdsByOpportunity);
    }

    public OpportunityTaskPreviewResult Preview(IReadOnlyList<Opportunity> opportunities, int plannerRound)
    {
        var baseCommit = gitClient.TryGetCurrentCommit(repoRoot);
        var proposedTasks = new List<TaskNode>();
        var proposedIdsByOpportunity = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var opportunity in opportunities)
        {
            var planningTask = BuildPlanningTask(opportunity, plannerRound, baseCommit);
            var scopeAnalysis = codeGraphQueryService?.AnalyzeScope(planningTask.Scope);
            var impactAnalysis = codeGraphQueryService?.AnalyzeImpact(planningTask.Scope);
            var executionTasks = taskDecomposer.DecomposePlanningTask(planningTask, baseCommit, [systemConfig.DefaultTestCommand], scopeAnalysis, impactAnalysis);
            proposedTasks.Add(planningTask);
            proposedTasks.AddRange(executionTasks);
            proposedIdsByOpportunity[opportunity.OpportunityId] = new[] { planningTask.TaskId }.Concat(executionTasks.Select(task => task.TaskId)).ToArray();
        }

        return new OpportunityTaskPreviewResult(proposedIdsByOpportunity, proposedTasks);
    }

    private TaskNode BuildPlanningTask(Opportunity opportunity, int plannerRound, string baseCommit)
    {
        const TaskType taskType = TaskType.Planning;
        if (!TaskTypePolicy.AllowPlannerGeneration(taskType))
        {
            throw new InvalidOperationException("Opportunity materialization must start with planner-generatable work.");
        }

        var planningTaskId = BuildPlanningTaskId(opportunity);
        var priority = MapPriority(opportunity.Severity);
        var metadata = new Dictionary<string, string>(opportunity.Metadata, StringComparer.Ordinal)
        {
            ["origin_opportunity_id"] = opportunity.OpportunityId,
            ["origin_opportunity_source"] = opportunity.Source.ToString(),
            ["origin_opportunity_fingerprint"] = opportunity.Fingerprint,
            ["planner_round"] = plannerRound.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return new TaskNode
        {
            TaskId = planningTaskId,
            Title = $"Plan {opportunity.Title}",
            Description = opportunity.Description,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Suggested,
            TaskType = taskType,
            Priority = priority,
            Source = "PLANNER_OPPORTUNITY",
            ProposalSource = MapProposalSource(opportunity.Source),
            ProposalReason = opportunity.Reason,
            ProposalConfidence = opportunity.Confidence,
            ProposalPriorityHint = priority,
            BaseCommit = baseCommit,
            Scope = opportunity.RelatedFiles.Count == 0 ? [".ai/opportunities/index.json"] : opportunity.RelatedFiles,
            Acceptance =
            [
                "opportunity is translated into explicit planning truth",
                "execution follow-up tasks are reviewed before promotion",
            ],
            Constraints =
            [
                "do not auto-promote planner-generated work to pending",
                "keep opportunity-derived work subordinate to review and autonomy policy",
            ],
            AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                planningTaskId,
                $"Plan {opportunity.Title}",
                opportunity.Description,
                cardId: null,
                [
                    "opportunity is translated into explicit planning truth",
                    "execution follow-up tasks are reviewed before promotion",
                ],
                [
                    "do not auto-promote planner-generated work to pending",
                    "keep opportunity-derived work subordinate to review and autonomy policy",
                ],
                new ValidationPlan
                {
                    Checks =
                    [
                        "planning task persists opportunity lineage",
                        "execution follow-up remains governed by task typing",
                    ],
                    ExpectedEvidence =
                    [
                        "planning task node exists under .ai/tasks/nodes/",
                        "derived execution tasks depend on the planning task",
                    ],
                }),
            Metadata = metadata,
            Validation = new ValidationPlan
            {
                Checks =
                [
                    "planning task persists opportunity lineage",
                    "execution follow-up remains governed by task typing",
                ],
                ExpectedEvidence =
                [
                    "planning task node exists under .ai/tasks/nodes/",
                    "derived execution tasks depend on the planning task",
                ],
            },
        };
    }

    private static void AddIfMissing(DomainTaskGraph graph, ICollection<TaskNode> additions, TaskNode task)
    {
        if (graph.Tasks.ContainsKey(task.TaskId))
        {
            return;
        }

        additions.Add(task);
        graph.AddOrReplace(task);
    }

    private static string BuildPlanningTaskId(Opportunity opportunity)
    {
        var stem = opportunity.Title
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('.', '-')
            .Replace('_', '-');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{opportunity.Source}|{opportunity.Fingerprint}"))).ToLowerInvariant()[..8];
        return $"T-PLAN-{stem}-{hash}";
    }

    private static TaskProposalSource MapProposalSource(OpportunitySource source)
    {
        return source switch
        {
            OpportunitySource.Refactoring => TaskProposalSource.RefactoringBacklog,
            OpportunitySource.FailureRecovery => TaskProposalSource.FailureRecovery,
            OpportunitySource.CodeGraph => TaskProposalSource.CodeGraphOpportunity,
            OpportunitySource.MemoryDrift => TaskProposalSource.MemoryAudit,
            OpportunitySource.TestCoverage => TaskProposalSource.TestCoverageOpportunity,
            _ => TaskProposalSource.PlannerGapDetection,
        };
    }

    private static string MapPriority(OpportunitySeverity severity)
    {
        return severity switch
        {
            OpportunitySeverity.Critical => "P1",
            OpportunitySeverity.High => "P1",
            OpportunitySeverity.Medium => "P2",
            _ => "P3",
        };
    }
}
