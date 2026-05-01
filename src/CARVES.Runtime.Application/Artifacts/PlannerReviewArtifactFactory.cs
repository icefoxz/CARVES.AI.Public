using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Artifacts;

public sealed class PlannerReviewArtifactFactory
{
    public PlannerReviewArtifact Create(
        TaskNode task,
        TaskRunReport report,
        PlannerReview review,
        TaskTransitionDecision transition)
    {
        return new PlannerReviewArtifact
        {
            TaskId = task.TaskId,
            Review = review,
            ResultingStatus = transition.NextStatus,
            TransitionReason = transition.Reason,
            PlannerComment = review.Reason,
            PatchSummary = DescribePatch(report.Patch),
            ResultCommit = report.ResultCommit,
            ValidationPassed = report.Validation.Passed,
            ValidationEvidence = report.Validation.Evidence,
            SafetyOutcome = report.SafetyDecision.Outcome,
            SafetyIssues = report.SafetyDecision.Issues.Select(issue => $"{issue.Code}: {issue.Message}").ToArray(),
            DecisionStatus = ResolveInitialDecisionStatus(transition, review),
            Writeback = new ReviewWritebackRecord
            {
                Applied = false,
                Summary = "Delegated repo writeback is pending review approval.",
            },
            DecisionDebt = review.DecisionDebt,
            RealityProjection = BuildRealityProjection(task, report, transition.NextStatus),
            ClosureBundle = ReviewClosureBundleFactory.Build(
                task,
                patchPaths: report.Patch.Paths,
                validationPassed: report.Validation.Passed,
                validationEvidence: report.Validation.Evidence,
                validationCommandResults: report.Validation.CommandResults,
                validationEvidenceCount: report.Validation.Evidence.Count,
                safetyOutcome: report.SafetyDecision.Outcome,
                safetyIssues: report.SafetyDecision.Issues.Select(issue => $"{issue.Code}: {issue.Message}").ToArray(),
                review,
                transition.NextStatus,
                writebackApplied: false,
                workerCompletionClaim: report.WorkerExecution.CompletionClaim,
                historicalWorkerFailureKind: null,
                existingBundle: null),
        };
    }

    public PlannerReviewArtifact RecordDecision(
        PlannerReviewArtifact artifact,
        TaskNode? task,
        PlannerReview review,
        DomainTaskStatus resultingStatus,
        string reason,
        string? resultCommit = null,
        ReviewWritebackRecord? writeback = null,
        IReadOnlyList<string>? closurePatchPaths = null,
        ReviewClosureHostValidationSummary? hostValidationOverride = null)
    {
        return new PlannerReviewArtifact
        {
            SchemaVersion = artifact.SchemaVersion,
            TaskId = artifact.TaskId,
            CapturedAt = artifact.CapturedAt,
            Review = review,
            ResultingStatus = resultingStatus,
            TransitionReason = artifact.TransitionReason,
            PlannerComment = artifact.PlannerComment,
            PatchSummary = artifact.PatchSummary,
            ResultCommit = resultCommit ?? artifact.ResultCommit,
            ValidationPassed = artifact.ValidationPassed,
            ValidationEvidence = artifact.ValidationEvidence,
            SafetyOutcome = artifact.SafetyOutcome,
            SafetyIssues = artifact.SafetyIssues,
            DecisionStatus = review.DecisionStatus,
            DecisionReason = reason,
            DecisionAt = DateTimeOffset.UtcNow,
            Writeback = writeback ?? artifact.Writeback,
            DecisionDebt = review.DecisionDebt,
            RealityProjection = UpdateRealityProjection(artifact.RealityProjection, resultingStatus, review, reason),
            ClosureBundle = ReviewClosureBundleFactory.Build(
                task,
                patchPaths: closurePatchPaths ?? ReviewClosureBundleFactory.ParsePatchPaths(artifact.PatchSummary),
                validationPassed: artifact.ValidationPassed,
                validationEvidence: artifact.ValidationEvidence,
                validationCommandResults: Array.Empty<CommandExecutionRecord>(),
                validationEvidenceCount: artifact.ValidationEvidence.Count,
                safetyOutcome: artifact.SafetyOutcome,
                safetyIssues: artifact.SafetyIssues,
                review,
                resultingStatus,
                writebackApplied: (writeback ?? artifact.Writeback).Applied,
                workerCompletionClaim: null,
                historicalWorkerFailureKind: null,
                existingBundle: artifact.ClosureBundle,
                hostValidationOverride: hostValidationOverride),
        };
    }

    public PlannerReviewArtifact RecordDecision(
        PlannerReviewArtifact artifact,
        PlannerReview review,
        DomainTaskStatus resultingStatus,
        string reason,
        string? resultCommit = null,
        ReviewWritebackRecord? writeback = null)
    {
        return RecordDecision(artifact, task: null, review, resultingStatus, reason, resultCommit, writeback);
    }

    public static IReadOnlyList<string> ParsePatchPaths(string patchSummary)
    {
        return ReviewClosureBundleFactory.ParsePatchPaths(patchSummary);
    }

    public static IReadOnlyList<string> ResolveClosurePatchPaths(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        IReadOnlyList<string> writebackFiles)
    {
        var artifactPatchPaths = ParsePatchPaths(reviewArtifact.PatchSummary);
        if (artifactPatchPaths.Count > 0)
        {
            return artifactPatchPaths;
        }

        return (workerArtifact?.Evidence.FilesWritten ?? Array.Empty<string>())
            .Concat(writebackFiles)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Where(path => !string.Equals(path, "(none)", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public MergeCandidateArtifact CreateMergeCandidate(PlannerReviewArtifact artifact, string reviewReason)
    {
        return new MergeCandidateArtifact
        {
            TaskId = artifact.TaskId,
            ReviewReason = reviewReason,
            PlannerComment = artifact.PlannerComment,
            ResultCommit = artifact.ResultCommit ?? string.Empty,
            PatchSummary = artifact.PatchSummary,
            ValidationPassed = artifact.ValidationPassed,
            SafetyOutcome = artifact.SafetyOutcome,
            Writeback = artifact.Writeback,
        };
    }

    private static string DescribePatch(PatchSummary patch)
    {
        var paths = patch.Paths.Count == 0 ? "(none)" : string.Join(", ", patch.Paths);
        return $"files={patch.FilesChanged}; added={patch.LinesAdded}; removed={patch.LinesRemoved}; estimated={patch.Estimated}; paths={paths}";
    }

    private static ReviewRealityProjection BuildRealityProjection(TaskNode task, TaskRunReport report, DomainTaskStatus resultingStatus)
    {
        var proofTarget = PlanningProofTargetMetadata.TryRead(task.Metadata);
        var promotionResult = "not_proven";
        var solidityClass = SolidityClass.Ghost;
        if (proofTarget is null)
        {
            promotionResult = "proof_target_missing";
        }
        else if (report.Validation.Passed && report.SafetyDecision.Outcome == SafetyOutcome.Allow)
        {
            if (resultingStatus == DomainTaskStatus.Review)
            {
                promotionResult = "review_ready";
                solidityClass = SolidityClass.Proto;
            }
            else if (resultingStatus is DomainTaskStatus.Completed or DomainTaskStatus.Merged)
            {
                promotionResult = "promoted";
                solidityClass = SolidityClass.Solid;
            }
            else
            {
                promotionResult = "proof_recorded";
                solidityClass = SolidityClass.Proto;
            }
        }

        return new ReviewRealityProjection
        {
            SolidityClass = solidityClass,
            PromotionResult = promotionResult,
            PlannedScope = task.Scope.Count == 0 ? task.Title : string.Join(", ", task.Scope),
            VerifiedOutcome = report.Validation.Passed
                ? string.Join("; ", report.Validation.Evidence.DefaultIfEmpty("validation passed"))
                : "Validation did not yet prove the scoped slice.",
            ProofTarget = proofTarget,
        };
    }

    private static ReviewRealityProjection UpdateRealityProjection(
        ReviewRealityProjection projection,
        DomainTaskStatus resultingStatus,
        PlannerReview review,
        string reason)
    {
        if (review.DecisionStatus == ReviewDecisionStatus.Approved
            && resultingStatus is DomainTaskStatus.Completed or DomainTaskStatus.Merged)
        {
            return new ReviewRealityProjection
            {
                SolidityClass = projection.ProofTarget is null ? SolidityClass.Ghost : SolidityClass.Solid,
                PromotionResult = projection.ProofTarget is null ? "approved_without_proof_target" : "promoted",
                PlannedScope = projection.PlannedScope,
                VerifiedOutcome = string.IsNullOrWhiteSpace(reason) ? projection.VerifiedOutcome : reason.Trim(),
                ProofTarget = projection.ProofTarget,
            };
        }

        if (review.DecisionStatus == ReviewDecisionStatus.ProvisionalAccepted)
        {
            return new ReviewRealityProjection
            {
                SolidityClass = projection.ProofTarget is null ? SolidityClass.Ghost : SolidityClass.Proto,
                PromotionResult = projection.ProofTarget is null ? "provisional_without_proof_target" : "provisional_accepted",
                PlannedScope = projection.PlannedScope,
                VerifiedOutcome = string.IsNullOrWhiteSpace(reason)
                    ? projection.VerifiedOutcome
                    : $"{projection.VerifiedOutcome} Provisional debt: {reason.Trim()}".Trim(),
                ProofTarget = projection.ProofTarget,
            };
        }

        if (review.DecisionStatus is ReviewDecisionStatus.Blocked or ReviewDecisionStatus.Superseded)
        {
            var decisionLabel = review.DecisionStatus == ReviewDecisionStatus.Blocked
                ? "Blocked"
                : "Superseded";
            var promotionResult = review.DecisionStatus == ReviewDecisionStatus.Blocked
                ? "blocked"
                : "superseded";
            return new ReviewRealityProjection
            {
                SolidityClass = projection.ProofTarget is null ? SolidityClass.Ghost : SolidityClass.Proto,
                PromotionResult = promotionResult,
                PlannedScope = projection.PlannedScope,
                VerifiedOutcome = string.IsNullOrWhiteSpace(reason)
                    ? projection.VerifiedOutcome
                    : $"{projection.VerifiedOutcome} {decisionLabel}: {reason.Trim()}".Trim(),
                ProofTarget = projection.ProofTarget,
            };
        }

        if (review.DecisionStatus is ReviewDecisionStatus.Reopened or ReviewDecisionStatus.Rejected)
        {
            var decisionLabel = review.DecisionStatus == ReviewDecisionStatus.Reopened
                ? "Reopened"
                : "Rejected";
            return new ReviewRealityProjection
            {
                SolidityClass = projection.ProofTarget is null ? SolidityClass.Ghost : SolidityClass.Proto,
                PromotionResult = review.DecisionStatus == ReviewDecisionStatus.Reopened
                    ? "reopened"
                    : "rejected",
                PlannedScope = projection.PlannedScope,
                VerifiedOutcome = string.IsNullOrWhiteSpace(reason)
                    ? projection.VerifiedOutcome
                    : $"{projection.VerifiedOutcome} {decisionLabel}: {reason.Trim()}".Trim(),
                ProofTarget = projection.ProofTarget,
            };
        }

        return projection;
    }

    private static ReviewDecisionStatus ResolveInitialDecisionStatus(TaskTransitionDecision transition, PlannerReview review)
    {
        if (review.DecisionStatus != ReviewDecisionStatus.NeedsAttention)
        {
            return review.DecisionStatus;
        }

        return transition.NextStatus switch
        {
            DomainTaskStatus.Review => ReviewDecisionStatus.PendingReview,
            DomainTaskStatus.Blocked => ReviewDecisionStatus.Blocked,
            DomainTaskStatus.Superseded => ReviewDecisionStatus.Superseded,
            _ => ReviewDecisionStatus.NeedsAttention,
        };
    }
}
