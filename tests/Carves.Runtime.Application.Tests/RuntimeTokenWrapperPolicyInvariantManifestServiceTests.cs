using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenWrapperPolicyInvariantManifestServiceTests
{
    [Fact]
    public void Persist_BuildsWorkerInvariantManifestFromTrustedInventory()
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
            RequestKindsCovered = ["worker"],
            CoverageLimitations = ["request_kind_not_covered:planner", "request_kind_not_covered:reviewer"],
            TopWrapperSurfaces =
            [
                new RuntimeTokenWrapperPolicySurfaceSummary
                {
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                    Role = "system",
                    SerializationKind = "developer_policy_text",
                    Producer = "worker_request_serializer.v1",
                    ShareP95 = 0.3164192708333333d,
                    TokensP95 = 646d,
                    PolicyCritical = true,
                    ManualReviewRequired = true,
                    CompressionAllowed = "structural_only",
                    RecommendedInventoryAction = "dedupe_and_request_kind_slice_review",
                }
            ],
        };

        var result = RuntimeTokenWrapperPolicyInvariantManifestService.Persist(paths, inventory, resultDate);

        Assert.True(result.Phase11WrapperInvariantManifestMayReferenceThisLine);
        Assert.Equal("wrapper_offline_validator", result.RequiredNextGate);
        Assert.False(result.Phase12WrapperCandidateAllowed);
        Assert.Equal("reprioritize_to_wrapper", result.Phase10Decision);
        Assert.Equal("wrapper_policy_shadow_offline", result.Phase10NextTrack);
        var surface = Assert.Single(result.SurfaceManifests);
        Assert.Equal("manifest:worker:system:$.instructions", surface.ManifestId);
        Assert.Equal("dedupe_then_request_kind_slice", surface.RecommendedCandidateStrategy);
        Assert.Equal("src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs", surface.SourceComponentPath);
        Assert.Equal("WorkerAiRequestFactory.BuildInstructions", surface.SourceAnchor);
        Assert.All(surface.Invariants, invariant =>
        {
            Assert.True(invariant.SemanticPreservationRequired);
            Assert.True(invariant.SaliencePreservationRequired);
            Assert.True(invariant.PriorityPreservationRequired);
            Assert.Equal("structural_only", invariant.CompressionAllowed);
            Assert.True(invariant.ManualReviewRequired);
        });
        Assert.Contains(surface.Invariants, item => item.InvariantId == "WRAP-WORKER-SCOPE-001");
        Assert.Contains(surface.Invariants, item => item.InvariantId == "WRAP-WORKER-SOURCE-007");
        Assert.Equal(7, surface.Invariants.Count);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_RejectsWrongTrack()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var inventory = new RuntimeTokenWrapperPolicyInventoryResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase11WrapperInventoryMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_tool_schema",
            Phase10NextTrack = "tool_schema_shadow_offline",
            TopWrapperSurfaces =
            [
                new RuntimeTokenWrapperPolicySurfaceSummary
                {
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                }
            ],
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTokenWrapperPolicyInvariantManifestService.Persist(workspace.Paths, inventory, resultDate));

        Assert.Contains("wrapper_policy_shadow_offline", error.Message, StringComparison.Ordinal);
    }
}
