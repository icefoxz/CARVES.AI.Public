using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerProposalAcceptanceService
{
    private readonly TaskGraphService taskGraphService;
    private readonly IOpportunityRepository opportunityRepository;

    public PlannerProposalAcceptanceService(TaskGraphService taskGraphService, IOpportunityRepository opportunityRepository)
    {
        this.taskGraphService = taskGraphService;
        this.opportunityRepository = opportunityRepository;
    }

    public PlannerProposalEnvelope Accept(PlannerProposalEnvelope envelope)
    {
        var graph = taskGraphService.Load();
        var idMap = envelope.Proposal.ProposedTasks.ToDictionary(
            task => task.TempId,
            task => string.IsNullOrWhiteSpace(task.TaskId) ? BuildTaskId(task, envelope.Proposal.ProposalId) : task.TaskId!,
            StringComparer.Ordinal);
        var acceptedTasks = new List<TaskNode>();
        var acceptedTaskIds = new List<string>();
        var rejectedTaskIds = new List<string>();

        foreach (var proposedTask in envelope.Proposal.ProposedTasks)
        {
            var taskId = idMap[proposedTask.TempId];
            if (graph.Tasks.ContainsKey(taskId))
            {
                rejectedTaskIds.Add(taskId);
                continue;
            }

            acceptedTasks.Add(new TaskNode
            {
                TaskId = taskId,
                Title = proposedTask.Title,
                Description = proposedTask.Description,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Suggested,
                TaskType = proposedTask.TaskType,
                Priority = proposedTask.Priority,
                Source = "PLANNER_ADAPTER",
                ProposalSource = ParseProposalSource(proposedTask.ProposalSource),
                ProposalReason = proposedTask.ProposalReason,
                ProposalConfidence = proposedTask.Confidence,
                ProposalPriorityHint = proposedTask.Priority,
                Dependencies = ResolveDependencies(proposedTask, envelope.Proposal.Dependencies, idMap),
                Scope = proposedTask.Scope,
                Acceptance = proposedTask.Acceptance.Count == 0 ? ["planner proposal accepted into governed truth"] : proposedTask.Acceptance,
                Constraints = proposedTask.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    taskId,
                    proposedTask.Title,
                    proposedTask.Description,
                    null,
                    proposedTask.Acceptance.Count == 0 ? ["planner proposal accepted into governed truth"] : proposedTask.Acceptance,
                    proposedTask.Constraints,
                    validation: null,
                    proposedTask.AcceptanceContract),
                Metadata = MergeMetadata(proposedTask.Metadata, proposedTask.ProofTarget, envelope),
            });
            acceptedTaskIds.Add(taskId);
        }

        if (acceptedTasks.Count > 0)
        {
            taskGraphService.AddTasks(acceptedTasks);
            MarkOriginatingOpportunities(acceptedTasks);
        }

        envelope.AcceptedTaskIds = acceptedTaskIds;
        envelope.RejectedTaskIds = rejectedTaskIds;
        envelope.AcceptanceStatus = acceptedTaskIds.Count == 0
            ? PlannerProposalAcceptanceStatus.Rejected
            : rejectedTaskIds.Count == 0
                ? PlannerProposalAcceptanceStatus.Accepted
                : PlannerProposalAcceptanceStatus.PartiallyAccepted;
        envelope.AcceptanceReason = envelope.AcceptanceStatus switch
        {
            PlannerProposalAcceptanceStatus.Accepted => $"Accepted {acceptedTaskIds.Count} planner-proposed tasks.",
            PlannerProposalAcceptanceStatus.PartiallyAccepted => $"Accepted {acceptedTaskIds.Count} planner-proposed tasks and rejected {rejectedTaskIds.Count} duplicates.",
            _ => "Planner proposal was rejected because all proposed tasks already exist or were invalid.",
        };
        envelope.Touch();
        return envelope;
    }

    public PlannerProposalEnvelope Reject(PlannerProposalEnvelope envelope, string reason)
    {
        envelope.AcceptanceStatus = PlannerProposalAcceptanceStatus.Rejected;
        envelope.AcceptanceReason = reason;
        envelope.AcceptedTaskIds = Array.Empty<string>();
        envelope.RejectedTaskIds = envelope.Proposal.ProposedTasks.Select(task => task.TaskId ?? task.TempId).ToArray();
        envelope.Touch();
        return envelope;
    }

    private void MarkOriginatingOpportunities(IReadOnlyList<TaskNode> acceptedTasks)
    {
        var grouped = acceptedTasks
            .Select(task => new
            {
                OpportunityId = task.Metadata.TryGetValue("origin_opportunity_id", out var opportunityId) ? opportunityId : null,
                TaskId = task.TaskId,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.OpportunityId))
            .GroupBy(item => item.OpportunityId!, item => item.TaskId, StringComparer.Ordinal);
        if (!grouped.Any())
        {
            return;
        }

        var snapshot = opportunityRepository.Load();
        var now = DateTimeOffset.UtcNow;
        foreach (var group in grouped)
        {
            var opportunity = snapshot.Items.FirstOrDefault(item => string.Equals(item.OpportunityId, group.Key, StringComparison.Ordinal));
            opportunity?.MarkMaterialized(group.Distinct(StringComparer.Ordinal).ToArray(), "accepted through planner proposal contract", now);
        }

        opportunityRepository.Save(new OpportunitySnapshot
        {
            Version = snapshot.Version,
            GeneratedAt = now,
            Items = snapshot.Items,
        });
    }

    private static IReadOnlyList<string> ResolveDependencies(
        PlannerProposedTask proposedTask,
        IReadOnlyList<PlannerProposedDependency> dependencies,
        IReadOnlyDictionary<string, string> idMap)
    {
        var direct = proposedTask.DependsOn.Select(item => idMap.TryGetValue(item, out var mapped) ? mapped : item);
        var additional = dependencies
            .Where(item => string.Equals(item.ToTaskId, proposedTask.TempId, StringComparison.Ordinal))
            .Select(item => idMap.TryGetValue(item.FromTaskId, out var mapped) ? mapped : item.FromTaskId);
        return direct.Concat(additional).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string> metadata, RealityProofTarget? proofTarget, PlannerProposalEnvelope envelope)
    {
        var merged = new Dictionary<string, string>(PlanningProofTargetMetadata.Merge(metadata, proofTarget), StringComparer.Ordinal)
        {
            ["planner_proposal_id"] = envelope.ProposalId,
            ["planner_adapter_id"] = envelope.AdapterId,
            ["planner_backend"] = envelope.Proposal.PlannerBackend,
        };
        return merged;
    }

    private static string BuildTaskId(PlannerProposedTask task, string proposalId)
    {
        var slug = task.Title
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('.', '-')
            .Replace('_', '-');
        return $"T-PLANNER-{slug}-{proposalId[^6..]}";
    }

    private static TaskProposalSource ParseProposalSource(string proposalSource)
    {
        if (string.IsNullOrWhiteSpace(proposalSource))
        {
            return TaskProposalSource.PlannerGapDetection;
        }

        var normalized = proposalSource.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "carddecomposition" => TaskProposalSource.CardDecomposition,
            "refactoringbacklog" => TaskProposalSource.RefactoringBacklog,
            "suggestedtask" => TaskProposalSource.SuggestedTask,
            "failurerecovery" => TaskProposalSource.FailureRecovery,
            "codegraphopportunity" => TaskProposalSource.CodeGraphOpportunity,
            "memoryaudit" => TaskProposalSource.MemoryAudit,
            "testcoverageopportunity" => TaskProposalSource.TestCoverageOpportunity,
            _ => TaskProposalSource.PlannerGapDetection,
        };
    }
}
