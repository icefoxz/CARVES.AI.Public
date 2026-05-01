using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2ActiveCanaryApprovalReviewServiceTests
{
    [Fact]
    public void Persist_ApprovesImplementationOnlyWhenReadinessPassed()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
        };
        var rollback = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-canary-rollback-plan-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-canary-rollback-plan-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CandidateVersion = "wrapper_candidate_20260421_worker_system___instructions",
            FallbackVersion = "original_worker_system_instructions",
            RollbackPlanReviewed = true,
            RollbackTestPlanDefined = true,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = ["worker"],
        };
        var cohort = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-non-inferiority-cohort-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/non-inferiority-cohort-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            NonInferiorityCohortFrozen = true,
            TaskIds = ["T-WORKER-001"],
        };
        var readiness = new RuntimeTokenPhase2ActiveCanaryReadinessReviewResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-readiness-review-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-readiness-review-2026-04-21.json",
            ReviewVerdict = "accepted_for_review_only",
            EnterActiveCanaryReviewAccepted = true,
            ActiveCanaryApproved = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            CandidateMarkdownArtifactPath = candidate.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidate.JsonArtifactPath,
            ReviewBundleMarkdownArtifactPath = candidate.ReviewBundleMarkdownArtifactPath,
            ReviewBundleJsonArtifactPath = candidate.ReviewBundleJsonArtifactPath,
            ManualReviewResolutionMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            ManualReviewResolutionJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            RequestKindSliceProofMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            RequestKindSliceProofJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            RollbackPlanMarkdownArtifactPath = rollback.MarkdownArtifactPath,
            RollbackPlanJsonArtifactPath = rollback.JsonArtifactPath,
            NonInferiorityCohortMarkdownArtifactPath = cohort.MarkdownArtifactPath,
            NonInferiorityCohortJsonArtifactPath = cohort.JsonArtifactPath,
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            TargetSurfaceReductionRatioP95 = 0.288,
            TargetSurfaceShareP95 = 0.316,
            ExpectedWholeRequestReductionP95 = 0.091,
            BlockingReasons = [],
        };

        var result = RuntimeTokenPhase2ActiveCanaryApprovalReviewService.Persist(
            workspace.Paths,
            readiness,
            candidate,
            reviewBundle,
            rollback,
            cohort,
            new RuntimeTokenWorkerWrapperCanaryService().DescribeMechanismContract(),
            resultDate);

        Assert.Equal("approved_for_canary_implementation_only", result.ReviewVerdict);
        Assert.True(result.PrerequisiteReviewPassed);
        Assert.True(result.CanaryImplementationAuthorized);
        Assert.False(result.CanaryExecutionAuthorized);
        Assert.False(result.ActiveCanaryApproved);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.Empty(result.BlockingReasons);
        Assert.Contains("separate_active_canary_execution_approval_required", result.ExecutionNotApprovedReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_BlocksWhenReadinessStillHasBlockers()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
        };
        var rollback = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            RollbackPlanReviewed = true,
            RollbackTestPlanDefined = true,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = ["worker"],
        };
        var cohort = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            NonInferiorityCohortFrozen = true,
        };
        var readiness = new RuntimeTokenPhase2ActiveCanaryReadinessReviewResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            ReviewVerdict = "accepted_for_review_only",
            EnterActiveCanaryReviewAccepted = true,
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            BlockingReasons = ["manual_review_unresolved"],
        };

        var result = RuntimeTokenPhase2ActiveCanaryApprovalReviewService.Persist(
            workspace.Paths,
            readiness,
            candidate,
            reviewBundle,
            rollback,
            cohort,
            new RuntimeTokenWorkerWrapperCanaryService().DescribeMechanismContract(),
            resultDate);

        Assert.Equal("blocked_for_active_canary", result.ReviewVerdict);
        Assert.False(result.PrerequisiteReviewPassed);
        Assert.False(result.CanaryImplementationAuthorized);
        Assert.False(result.CanaryExecutionAuthorized);
        Assert.Contains("manual_review_unresolved", result.BlockingReasons);
    }
}
