using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2RequestKindSliceProofServiceTests
{
    [Fact]
    public void Persist_BuildsWorkerScopedCrossKindProofWithoutRemovingPolicyCriticalFragments()
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
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = candidate.CandidateSurfaceId,
                    RequestKind = "worker",
                    PolicyCritical = true,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            Title = "Governed worker scope boundary",
                        },
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-STOP-006",
                            Title = "Treat stop conditions as hard preflight boundaries",
                        }
                    ],
                }
            ],
        };
        var manualReviewResolution = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            UnresolvedReviewCount = 0,
            FailCount = 0,
            ReviewItems =
            [
                new RuntimeTokenPhase2ManualReviewResolutionItem
                {
                    ReviewItemId = "review:scope",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                    ReviewResult = "pass",
                },
                new RuntimeTokenPhase2ManualReviewResolutionItem
                {
                    ReviewItemId = "review:stop",
                    InvariantId = "WRAP-WORKER-STOP-006",
                    ReviewResult = "pass",
                }
            ],
        };

        var result = RuntimeTokenPhase2RequestKindSliceProofService.Persist(
            workspace.Paths,
            candidate,
            manifest,
            manualReviewResolution,
            resultDate);

        Assert.True(result.CrossKindProofAvailable);
        Assert.Equal("proof_available_for_worker_only_canary_scope", result.CrossKindProofVerdict);
        Assert.Equal(["worker"], result.CanaryRequestKindAllowlist);
        Assert.Equal(2, result.PolicyCriticalFragmentCount);
        Assert.Equal(0, result.PolicyCriticalFragmentRemovedCount);
        Assert.Contains(result.MatrixEntries, item =>
            string.Equals(item.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(item.MatrixStatus, "preserved_in_candidate_scope", StringComparison.Ordinal)
            && string.Equals(item.ManualReviewStatus, "pass", StringComparison.Ordinal));
        Assert.Contains(result.MatrixEntries, item =>
            string.Equals(item.RequestKind, "planner", StringComparison.Ordinal)
            && string.Equals(item.MatrixStatus, "out_of_scope_not_removed", StringComparison.Ordinal)
            && !item.RemovedFromRequestKind);
        Assert.Contains(result.MatrixEntries, item =>
            string.Equals(item.RequestKind, "operator_readback", StringComparison.Ordinal)
            && string.Equals(item.MatrixStatus, "not_applicable", StringComparison.Ordinal));
        Assert.Empty(result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
