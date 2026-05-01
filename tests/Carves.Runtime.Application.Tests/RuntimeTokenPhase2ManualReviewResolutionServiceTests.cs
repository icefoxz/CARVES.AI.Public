using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2ManualReviewResolutionServiceTests
{
    [Fact]
    public void Persist_ResolvesAllManualReviewItemsWithoutCandidateChange()
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
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateTextPreview = """
                You are CARVES.Runtime's governed worker.

                Hard boundaries
                - Stay inside scope; respect sandbox and approval policy; edit only allowed files.
                - If the task is already bounded, do not ask for confirmation, preferred paths, or output format; choose a reasonable default and complete the task in one pass.

                Shell rules (Windows PowerShell)
                - Environment: Windows PowerShell only.
                - Do not use bash-only edit syntax such as `ApplyPatch <<'PATCH'`.

                Validation boundary
                - CARVES runs formal build/test validation after you return.
                - Do not run `dotnet restore`, `dotnet build`, or `dotnet test` unless the task explicitly asks for toolchain diagnosis.

                Execution budget
                - Max files changed: 12.
                - Max lines changed: 400.
                - Max shell commands: 6.
                - If the change is likely to exceed budget, narrow the slice or return a bounded assessment that the task must be split.

                Stop conditions
                - predicted_patch_exceeds_budget
                """,
            Samples =
            [
                new RuntimeTokenWrapperCandidateSampleResult
                {
                    TaskId = "T-CARD-962-001",
                    SourceGroundingIncluded = false,
                }
            ],
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            EnterActiveCanaryReviewBundleReady = true,
            ManualReviewQueue =
            [
                new RuntimeTokenWrapperCandidateManualReviewItem
                {
                    ReviewId = "review:scope",
                    ManifestId = "manifest:worker:system:$.instructions",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                },
                new RuntimeTokenWrapperCandidateManualReviewItem
                {
                    ReviewId = "review:source",
                    ManifestId = "manifest:worker:system:$.instructions",
                    InvariantId = "WRAP-WORKER-SOURCE-007",
                }
            ],
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        },
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SOURCE-007",
                            InvariantClass = "source_grounding",
                            Title = "Source identifiers must stay concrete when grounding is required",
                        }
                    ],
                }
            ],
        };

        var result = RuntimeTokenPhase2ManualReviewResolutionService.Persist(workspace.Paths, candidate, reviewBundle, manifest, resultDate);

        Assert.Equal("resolved_without_candidate_change", result.ResolutionVerdict);
        Assert.Equal(2, result.ResolvedReviewCount);
        Assert.Equal(0, result.UnresolvedReviewCount);
        Assert.Equal(0, result.FailCount);
        Assert.Equal(0, result.CandidateChangeRequiredCount);
        Assert.Equal(2, result.SemanticPreservationPassCount);
        Assert.Equal(2, result.SaliencePreservationPassCount);
        Assert.Equal(2, result.PriorityPreservationPassCount);
        Assert.Equal(2, result.ApplicabilityPassCount);
        Assert.All(result.ReviewItems, item =>
        {
            Assert.Equal("pass", item.ReviewResult);
            Assert.False(item.CandidateChangeRequired);
            Assert.False(item.Blocking);
        });
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
