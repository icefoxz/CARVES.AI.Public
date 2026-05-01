using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2RollbackPlanFreezeServiceTests
{
    [Fact]
    public void Persist_FreezesDefaultOffRollbackPlanForWorkerScope()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
        };
        var proof = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CrossKindProofVerdict = "proof_available_for_worker_only_canary_scope",
            CrossKindProofAvailable = true,
            CanaryRequestKindAllowlist = ["worker"],
            PolicyCriticalFragmentCount = 7,
            PolicyCriticalFragmentRemovedCount = 0,
        };

        var result = RuntimeTokenPhase2RollbackPlanFreezeService.Persist(
            workspace.Paths,
            candidate,
            proof,
            resultDate);

        Assert.True(result.RollbackPlanReviewed);
        Assert.True(result.RollbackTestPlanDefined);
        Assert.False(result.DefaultEnabled);
        Assert.True(result.GlobalKillSwitch);
        Assert.True(result.PerRequestKindFallback);
        Assert.True(result.PerSurfaceFallback);
        Assert.Equal(["worker"], result.CanaryRequestKindAllowlist);
        Assert.Equal("original_worker_system_instructions", result.FallbackVersion);
        Assert.Contains("hard_fail_count_gt_0", result.AutomaticRollbackTriggers);
        Assert.Empty(result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
