using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private static TaskNode ApplyReviewDecision(
        TaskNode task,
        DomainTaskStatus resultingStatus,
        PlannerReview review,
        AcceptanceContractHumanDecision? contractDecision = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var mergedMetadata = metadata is null
            ? new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        var projectedContract = contractDecision is null
            ? task.AcceptanceContract
            : AcceptanceContractStatusProjector.ApplyHumanDecision(task.AcceptanceContract, contractDecision.Value);

        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = resultingStatus,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = projectedContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = mergedMetadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = review,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static ReviewDecisionDebt BuildDecisionDebt(string reason)
    {
        return BuildDecisionDebt(reason, ReviewEvidenceAssessment.Satisfied);
    }

    private static ReviewDecisionDebt BuildDecisionDebt(string reason, ReviewEvidenceAssessment evidenceAssessment)
    {
        var summary = reason.Trim();
        if (!evidenceAssessment.IsSatisfied)
        {
            var evidenceSummary = evidenceAssessment.SummarizeMissingRequirements();
            summary = string.IsNullOrWhiteSpace(summary)
                ? $"Missing acceptance-contract evidence: {evidenceSummary}."
                : $"{summary} Missing acceptance-contract evidence: {evidenceSummary}.";
        }

        var followUpActions = new List<string>
        {
            "Capture the remaining gap as explicit follow-up work before closing the acceptance contract.",
            "Return to full review once the provisional debt is cleared.",
        };
        followUpActions.AddRange(evidenceAssessment.BuildFollowUpActions());

        return new ReviewDecisionDebt
        {
            Summary = summary,
            FollowUpActions = followUpActions
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            RequiresFollowUpReview = true,
            RecordedAt = DateTimeOffset.UtcNow,
        };
    }

    private static bool AllowsProvisionalAcceptance(TaskNode task)
    {
        var policy = task.AcceptanceContract?.HumanReview;
        return policy is not null
               && policy.ProvisionalAllowed
               && policy.Decisions.Contains(AcceptanceContractHumanDecision.ProvisionalAccept);
    }

    private static ReviewDecisionStatus ResolveCurrentReviewDecision(TaskNode task, PlannerReviewArtifact reviewArtifact)
    {
        return task.PlannerReview.DecisionStatus == ReviewDecisionStatus.NeedsAttention
            ? reviewArtifact.DecisionStatus
            : task.PlannerReview.DecisionStatus;
    }

    private static bool AllowsReviewReopen(TaskNode task, PlannerReviewArtifact reviewArtifact)
    {
        var decision = ResolveCurrentReviewDecision(task, reviewArtifact);
        return task.Status switch
        {
            DomainTaskStatus.Review => decision == ReviewDecisionStatus.ProvisionalAccepted,
            DomainTaskStatus.Completed or DomainTaskStatus.Merged => decision is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted,
            _ => false,
        };
    }
}
