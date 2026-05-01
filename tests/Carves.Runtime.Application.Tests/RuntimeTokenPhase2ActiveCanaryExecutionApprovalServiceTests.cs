using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2ActiveCanaryExecutionApprovalServiceTests
{
    [Fact]
    public void Persist_ApprovesExecutionWhenImplementationReviewAndMechanismContractPass()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var approvalReview = new RuntimeTokenPhase2ActiveCanaryApprovalReviewResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-approval-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-approval-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ApprovalScope = "limited_explicit_allowlist",
            CanaryRequestKindAllowlist = ["worker"],
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            RollbackPlanFrozen = true,
            NonInferiorityCohortFrozen = true,
            ReviewVerdict = "approved_for_canary_implementation_only",
            PrerequisiteReviewPassed = true,
            CanaryImplementationAuthorized = true,
            ExpectedWholeRequestReductionP95 = 0.091,
            BlockingReasons = [],
        };

        var result = RuntimeTokenPhase2ActiveCanaryExecutionApprovalService.Persist(
            workspace.Paths,
            approvalReview,
            new RuntimeTokenWorkerWrapperCanaryService().DescribeMechanismContract(),
            resultDate);

        Assert.Equal("approved_for_active_canary_execution", result.ReviewVerdict);
        Assert.True(result.ActiveCanaryApproved);
        Assert.True(result.CanaryExecutionAuthorized);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.Empty(result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_BlocksExecutionWhenMechanismContractDoesNotMatchApprovedVersion()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var approvalReview = new RuntimeTokenPhase2ActiveCanaryApprovalReviewResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ApprovalScope = "limited_explicit_allowlist",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            RollbackPlanFrozen = true,
            NonInferiorityCohortFrozen = true,
            ReviewVerdict = "approved_for_canary_implementation_only",
            PrerequisiteReviewPassed = true,
            CanaryImplementationAuthorized = true,
        };
        var mismatchedContract = new RuntimeTokenWorkerWrapperCanaryMechanismContract
        {
            TargetSurface = "worker:system:$.instructions",
            RequestKind = "worker",
            ApprovalScope = "limited_explicit_allowlist",
            CandidateVersion = "wrong_version",
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            DefaultOffSupported = true,
            GlobalKillSwitchSupported = true,
            RequestKindAllowlistSupported = true,
            SurfaceAllowlistSupported = true,
            CandidateVersionPinSupported = true,
            EnvironmentVariables = [RuntimeTokenWorkerWrapperCanaryService.CanaryEnabledEnvironmentVariable]
        };

        var result = RuntimeTokenPhase2ActiveCanaryExecutionApprovalService.Persist(
            workspace.Paths,
            approvalReview,
            mismatchedContract,
            resultDate);

        Assert.Equal("blocked_for_active_canary_execution", result.ReviewVerdict);
        Assert.False(result.ActiveCanaryApproved);
        Assert.False(result.CanaryExecutionAuthorized);
        Assert.Contains("mechanism_candidate_version_mismatch", result.BlockingReasons);
    }
}
