using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenWrapperOfflineValidatorServiceTests
{
    [Fact]
    public void Persist_BuildsOfflineValidatorResultAndUnlocksPhase12StartOnly()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var inventory = new RuntimeTokenWrapperPolicyInventoryResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-inventory-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-inventory-result-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase11WrapperInventoryMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            RequestKindSummaries =
            [
                new RuntimeTokenWrapperPolicyRequestKindSummary
                {
                    RequestKind = "worker",
                    RequestCount = 5,
                    WrapperSurfaceCount = 1,
                    WrapperTokensP95 = 646,
                    WrapperShareP95 = 0.316,
                }
            ],
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = inventory.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            InventoryMarkdownArtifactPath = inventory.MarkdownArtifactPath,
            InventoryJsonArtifactPath = inventory.JsonArtifactPath,
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase11WrapperInvariantManifestMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            RequiredNextGate = "wrapper_offline_validator",
            RequestKindsCovered = ["worker"],
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                    ShareP95 = 0.316,
                    TokensP95 = 646,
                    RecommendedCandidateStrategy = "dedupe_then_request_kind_slice",
                    RequiredValidatorChecks = ["wrapper_invariant_coverage", "semantic_preservation", "salience_preservation", "priority_preservation", "manual_review_resolution"],
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
                            InvariantId = "WRAP-WORKER-STOP-006",
                            InvariantClass = "stop_conditions",
                            Title = "Stop conditions remain hard preflight boundaries",
                        }
                    ],
                }
            ],
        };
        var recollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = inventory.CohortId,
                RequestKinds = ["worker"],
            },
            RecollectedTaskCount = 5,
            TaskIds = ["T-CARD-962-001", "T-CARD-963-001", "T-CARD-964-001", "T-CARD-965-002", "T-CARD-966-001"],
        };

        var result = RuntimeTokenWrapperOfflineValidatorService.Persist(paths, manifest, inventory, recollect, resultDate);

        Assert.True(result.Phase11WrapperValidatorMayReferenceThisLine);
        Assert.Equal("ready_for_phase12_wrapper_candidate_design", result.ValidatorVerdict);
        Assert.True(result.Phase12WrapperCandidateMayStart);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.ActiveCanaryAllowed);
        var surface = Assert.Single(result.SurfaceResults);
        Assert.Equal("source_echo_baseline", surface.ComparisonMode);
        Assert.True(surface.SchemaValidityPass);
        Assert.Equal("pass", surface.InvariantCoverageStatus);
        Assert.Equal("pass", surface.SemanticPreservationStatus);
        Assert.Equal("pass", surface.SaliencePreservationStatus);
        Assert.Equal("pass", surface.PriorityPreservationStatus);
        Assert.Equal(0d, surface.TokenDeltaP95);
        var reviewItems = result.ManualReviewQueue;
        Assert.Equal(2, reviewItems.Count);
        Assert.All(reviewItems, item =>
        {
            Assert.Equal("pending_candidate_diff", item.ReviewStatus);
            Assert.False(item.BlocksPhase11Completion);
            Assert.True(item.BlocksPhase12Signoff);
        });
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_RejectsWrongManifestGate()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase11WrapperInvariantManifestMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            RequiredNextGate = "wrong_gate",
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                }
            ],
        };
        var inventory = new RuntimeTokenWrapperPolicyInventoryResult
        {
            ResultDate = resultDate,
            CohortId = manifest.CohortId,
            TrustLineClassification = manifest.TrustLineClassification,
            Phase11WrapperInventoryMayReferenceThisLine = true,
            Phase10Decision = manifest.Phase10Decision,
            Phase10NextTrack = manifest.Phase10NextTrack,
        };
        var recollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = manifest.CohortId,
                RequestKinds = ["worker"],
            },
            RecollectedTaskCount = 1,
            TaskIds = ["T-CARD-962-001"],
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTokenWrapperOfflineValidatorService.Persist(workspace.Paths, manifest, inventory, recollect, resultDate));

        Assert.Contains("wrapper_offline_validator", error.Message, StringComparison.Ordinal);
    }
}
