using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ReviewEvidenceProjectionService
{
    private readonly ReviewEvidenceGateService reviewEvidenceGateService;
    private readonly ReviewWritebackService reviewWritebackService;

    public ReviewEvidenceProjectionService(string repoRoot, IGitClient gitClient)
        : this(new ReviewEvidenceGateService(), new ReviewWritebackService(repoRoot, gitClient))
    {
    }

    public ReviewEvidenceProjectionService(
        ReviewEvidenceGateService reviewEvidenceGateService,
        ReviewWritebackService reviewWritebackService)
    {
        this.reviewEvidenceGateService = reviewEvidenceGateService;
        this.reviewWritebackService = reviewWritebackService;
    }

    public ReviewEvidenceGateProjection Build(
        TaskNode task,
        PlannerReviewArtifact? reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        if (reviewArtifact is null)
        {
            return ReviewEvidenceGateProjection.Unavailable("Review artifact is not available.");
        }

        var requiredEvidence = task.AcceptanceContract?.EvidenceRequired
            .Select(FormatRequirementLabel)
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();

        if (reviewArtifact.Writeback.Applied)
        {
            var afterAppliedWriteback = reviewEvidenceGateService.EvaluateAfterWriteback(
                task,
                reviewArtifact,
                workerArtifact,
                reviewArtifact.Writeback);

            return new ReviewEvidenceGateProjection(
                Status: afterAppliedWriteback.IsSatisfied ? "writeback_applied" : "post_writeback_gap",
                CanFinalApprove: false,
                CanWritebackProceed: false,
                WillApplyWriteback: false,
                WillCaptureResultCommit: !string.IsNullOrWhiteSpace(reviewArtifact.Writeback.ResultCommit),
                Summary: BuildAppliedWritebackSummary(afterAppliedWriteback),
                RequiredEvidence: requiredEvidence,
                MissingBeforeWriteback: Array.Empty<ReviewEvidenceGap>(),
                MissingAfterWriteback: afterAppliedWriteback.MissingRequirements,
                FollowUpActions: afterAppliedWriteback.IsSatisfied
                    ? Array.Empty<string>()
                    : afterAppliedWriteback.BuildFollowUpActions(),
                WritebackFailureMessage: null)
            {
                ClosureStatus = reviewArtifact.ClosureBundle.ClosureDecision.Status,
                ClosureDecision = reviewArtifact.ClosureBundle.ClosureDecision.Decision,
                ClosureWritebackAllowed = reviewArtifact.ClosureBundle.ClosureDecision.WritebackAllowed,
                ClosureBlockers = reviewArtifact.ClosureBundle.ClosureDecision.Blockers,
                CompletionClaimStatus = reviewArtifact.ClosureBundle.CompletionClaim.Status,
                CompletionClaimRequired = reviewArtifact.ClosureBundle.CompletionClaim.Required,
                CompletionClaimPresentFields = reviewArtifact.ClosureBundle.CompletionClaim.PresentFields,
                CompletionClaimMissingFields = reviewArtifact.ClosureBundle.CompletionClaim.MissingFields,
                CompletionClaimEvidencePaths = reviewArtifact.ClosureBundle.CompletionClaim.EvidencePaths,
                CompletionClaimNextRecommendation = reviewArtifact.ClosureBundle.CompletionClaim.NextRecommendation,
                CompletionClaimSummary = BuildCompletionClaimSummary(reviewArtifact.ClosureBundle.CompletionClaim),
                HostValidationStatus = reviewArtifact.ClosureBundle.HostValidation.Status,
                HostValidationRequired = reviewArtifact.ClosureBundle.HostValidation.Required,
                HostValidationReasonCode = reviewArtifact.ClosureBundle.HostValidation.ReasonCode,
                HostValidationBlockers = reviewArtifact.ClosureBundle.HostValidation.Blockers,
                HostValidationSummary = BuildHostValidationSummary(reviewArtifact.ClosureBundle.HostValidation),
            };
        }

        var beforeWriteback = reviewEvidenceGateService.EvaluateBeforeWriteback(task, reviewArtifact, workerArtifact);
        var writebackPreview = reviewWritebackService.Preview(reviewArtifact, workerArtifact);
        var afterWriteback = reviewEvidenceGateService.EvaluateProjectedAfterWriteback(
            task,
            reviewArtifact,
            workerArtifact,
            writebackPreview.CanProceed
                ? writebackPreview.ToEvidenceProjection()
                : new ReviewWritebackEvidenceProjection(false, false, Array.Empty<string>()));
        var projectedClosureBundle = BuildProjectedClosureBundle(task, reviewArtifact, workerArtifact, writebackPreview);
        var closureDecision = projectedClosureBundle.ClosureDecision;

        return new ReviewEvidenceGateProjection(
            Status: ResolveStatus(beforeWriteback, writebackPreview, afterWriteback, closureDecision),
            CanFinalApprove: writebackPreview.CanProceed && afterWriteback.IsSatisfied && closureDecision.WritebackAllowed,
            CanWritebackProceed: writebackPreview.CanProceed,
            WillApplyWriteback: writebackPreview.WillApply,
            WillCaptureResultCommit: writebackPreview.WillCaptureResultCommit,
            Summary: BuildSummary(beforeWriteback, writebackPreview, afterWriteback, closureDecision, requiredEvidence.Length > 0),
            RequiredEvidence: requiredEvidence,
            MissingBeforeWriteback: beforeWriteback.MissingRequirements,
            MissingAfterWriteback: afterWriteback.MissingRequirements,
            FollowUpActions: BuildFollowUpActions(afterWriteback, writebackPreview, closureDecision),
            WritebackFailureMessage: writebackPreview.FailureMessage)
        {
            ClosureStatus = closureDecision.Status,
            ClosureDecision = closureDecision.Decision,
            ClosureWritebackAllowed = closureDecision.WritebackAllowed,
            ClosureBlockers = closureDecision.Blockers,
            CompletionClaimStatus = reviewArtifact.ClosureBundle.CompletionClaim.Status,
            CompletionClaimRequired = reviewArtifact.ClosureBundle.CompletionClaim.Required,
            CompletionClaimPresentFields = reviewArtifact.ClosureBundle.CompletionClaim.PresentFields,
            CompletionClaimMissingFields = reviewArtifact.ClosureBundle.CompletionClaim.MissingFields,
            CompletionClaimEvidencePaths = reviewArtifact.ClosureBundle.CompletionClaim.EvidencePaths,
            CompletionClaimNextRecommendation = reviewArtifact.ClosureBundle.CompletionClaim.NextRecommendation,
            CompletionClaimSummary = BuildCompletionClaimSummary(reviewArtifact.ClosureBundle.CompletionClaim),
            HostValidationStatus = projectedClosureBundle.HostValidation.Status,
            HostValidationRequired = projectedClosureBundle.HostValidation.Required,
            HostValidationReasonCode = projectedClosureBundle.HostValidation.ReasonCode,
            HostValidationBlockers = projectedClosureBundle.HostValidation.Blockers,
            HostValidationSummary = BuildHostValidationSummary(projectedClosureBundle.HostValidation),
        };
    }

    private static ReviewClosureBundle BuildProjectedClosureBundle(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        ReviewWritebackPreview writebackPreview)
    {
        var closurePatchPaths = PlannerReviewArtifactFactory.ResolveClosurePatchPaths(
            reviewArtifact,
            workerArtifact,
            writebackPreview.Files);
        var closurePreview = new PlannerReviewArtifactFactory().RecordDecision(
            reviewArtifact,
            task,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Projected final approval for review evidence readback.",
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = true,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
            DomainTaskStatus.Completed,
            "Projected final approval for review evidence readback.",
            closurePatchPaths: closurePatchPaths);
        return closurePreview.ClosureBundle;
    }

    private static string ResolveStatus(
        ReviewEvidenceAssessment beforeWriteback,
        ReviewWritebackPreview writebackPreview,
        ReviewEvidenceAssessment afterWriteback,
        ReviewClosureDecision closureDecision)
    {
        if (!beforeWriteback.IsSatisfied)
        {
            return "pre_writeback_gap";
        }

        if (!writebackPreview.CanProceed)
        {
            return "writeback_blocked";
        }

        if (!afterWriteback.IsSatisfied)
        {
            return "post_writeback_gap";
        }

        if (!closureDecision.WritebackAllowed)
        {
            return "closure_blocked";
        }

        return "final_ready";
    }

    private static string BuildSummary(
        ReviewEvidenceAssessment beforeWriteback,
        ReviewWritebackPreview writebackPreview,
        ReviewEvidenceAssessment afterWriteback,
        ReviewClosureDecision closureDecision,
        bool hasExplicitEvidenceRequirements)
    {
        if (!beforeWriteback.IsSatisfied)
        {
            var summary = $"Final approval blocked before writeback: missing {beforeWriteback.SummarizeMissingRequirements()}.";
            if (!writebackPreview.CanProceed && !string.IsNullOrWhiteSpace(writebackPreview.FailureMessage))
            {
                summary += $" Writeback preview is also blocked: {TrimTrailingPeriod(writebackPreview.FailureMessage)}.";
            }

            return summary;
        }

        if (!writebackPreview.CanProceed)
        {
            return $"Final approval blocked at writeback preview: {TrimTrailingPeriod(writebackPreview.FailureMessage ?? "delegated writeback cannot proceed")}.";
        }

        if (!afterWriteback.IsSatisfied)
        {
            return $"Final approval blocked after writeback projection: missing {afterWriteback.SummarizeMissingRequirements()}.";
        }

        if (!closureDecision.WritebackAllowed)
        {
            return $"Final approval blocked by closure decision: {FormatClosureBlockers(closureDecision)}.";
        }

        if (!writebackPreview.WillApply)
        {
            return hasExplicitEvidenceRequirements
                ? "Final approval can proceed; required acceptance evidence is already present."
                : "Final approval can proceed; no additional acceptance evidence or delegated writeback is required.";
        }

        return hasExplicitEvidenceRequirements
            ? "Final approval can proceed; delegated writeback can satisfy the remaining acceptance evidence."
            : "Final approval can proceed; delegated writeback is materializable.";
    }

    private static string BuildAppliedWritebackSummary(ReviewEvidenceAssessment afterAppliedWriteback)
    {
        return afterAppliedWriteback.IsSatisfied
            ? "Review already approved and writeback is applied; required acceptance evidence is satisfied."
            : $"Review writeback is applied, but required evidence remains missing: {afterAppliedWriteback.SummarizeMissingRequirements()}.";
    }

    private static IReadOnlyList<string> BuildFollowUpActions(
        ReviewEvidenceAssessment afterWriteback,
        ReviewWritebackPreview writebackPreview,
        ReviewClosureDecision closureDecision)
    {
        var actions = new List<string>();
        if (!afterWriteback.IsSatisfied)
        {
            actions.AddRange(afterWriteback.BuildFollowUpActions());
        }

        if (!writebackPreview.CanProceed)
        {
            actions.Add("Resolve delegated writeback availability before final approval.");
        }

        if (!closureDecision.WritebackAllowed)
        {
            actions.Add($"Resolve closure decision blockers before final approval: {FormatClosureBlockers(closureDecision)}.");
        }

        return actions
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatClosureBlockers(ReviewClosureDecision closureDecision)
    {
        return closureDecision.Blockers.Count == 0
            ? $"status={closureDecision.Status}; decision={closureDecision.Decision}"
            : string.Join(", ", closureDecision.Blockers);
    }

    private static string FormatRequirementLabel(AcceptanceContractEvidenceRequirement requirement)
    {
        return string.IsNullOrWhiteSpace(requirement.Description)
            ? requirement.Type.Trim()
            : $"{requirement.Type.Trim()} ({requirement.Description.Trim()})";
    }

    private static string BuildCompletionClaimSummary(ReviewClosureCompletionClaimSummary claim)
    {
        if (!claim.Required && string.Equals(claim.Status, "not_recorded", StringComparison.Ordinal))
        {
            return "Worker completion claim was not recorded; Host validation and Review closure remain authoritative.";
        }

        if (claim.MissingFields.Count > 0)
        {
            return $"Worker completion claim {claim.Status}; missing fields: {string.Join(", ", claim.MissingFields)}. Claim is not lifecycle truth.";
        }

        if (claim.Required)
        {
            return $"Worker completion claim {claim.Status}; Host validation and Review closure remain authoritative.";
        }

        return $"Worker completion claim {claim.Status}; claim is not lifecycle truth.";
    }

    private static string BuildHostValidationSummary(ReviewClosureHostValidationSummary hostValidation)
    {
        if (!hostValidation.Required)
        {
            return $"{hostValidation.Message} Host validation summary is ReviewBundle evidence only.";
        }

        if (hostValidation.Blockers.Count > 0)
        {
            return $"Host validation {hostValidation.Status}; blockers: {string.Join(", ", hostValidation.Blockers)}. ReviewBundle evidence does not write lifecycle truth.";
        }

        return $"Host validation {hostValidation.Status}; worker claim remains candidate evidence until Review closure and Host writeback.";
    }

    private static string TrimTrailingPeriod(string value)
    {
        return value.Trim().TrimEnd('.');
    }
}

public sealed record ReviewEvidenceGateProjection(
    string Status,
    bool CanFinalApprove,
    bool CanWritebackProceed,
    bool WillApplyWriteback,
    bool WillCaptureResultCommit,
    string Summary,
    IReadOnlyList<string> RequiredEvidence,
    IReadOnlyList<ReviewEvidenceGap> MissingBeforeWriteback,
    IReadOnlyList<ReviewEvidenceGap> MissingAfterWriteback,
    IReadOnlyList<string> FollowUpActions,
    string? WritebackFailureMessage)
{
    public string ClosureStatus { get; init; } = "not_evaluated";

    public string ClosureDecision { get; init; } = "block_writeback";

    public bool ClosureWritebackAllowed { get; init; }

    public IReadOnlyList<string> ClosureBlockers { get; init; } = Array.Empty<string>();

    public string CompletionClaimStatus { get; init; } = "not_recorded";

    public bool CompletionClaimRequired { get; init; }

    public IReadOnlyList<string> CompletionClaimPresentFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CompletionClaimMissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CompletionClaimEvidencePaths { get; init; } = Array.Empty<string>();

    public string CompletionClaimNextRecommendation { get; init; } = string.Empty;

    public string CompletionClaimSummary { get; init; } = "Worker completion claim was not recorded; Host validation and Review closure remain authoritative.";

    public string HostValidationStatus { get; init; } = "not_evaluated";

    public bool HostValidationRequired { get; init; }

    public string HostValidationReasonCode { get; init; } = "not_evaluated";

    public IReadOnlyList<string> HostValidationBlockers { get; init; } = Array.Empty<string>();

    public string HostValidationSummary { get; init; } = "Host validation has not been evaluated for this review bundle.";

    public static ReviewEvidenceGateProjection Unavailable(string summary)
    {
        return new ReviewEvidenceGateProjection(
            Status: "unavailable",
            CanFinalApprove: false,
            CanWritebackProceed: false,
            WillApplyWriteback: false,
            WillCaptureResultCommit: false,
            Summary: summary,
            RequiredEvidence: Array.Empty<string>(),
            MissingBeforeWriteback: Array.Empty<ReviewEvidenceGap>(),
            MissingAfterWriteback: Array.Empty<ReviewEvidenceGap>(),
            FollowUpActions: Array.Empty<string>(),
            WritebackFailureMessage: summary);
    }
}
