using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackagingProofFederationMaturityServiceTests
{
    [Fact]
    public void Build_ProjectsPackagingProofAndFederationMaturity()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# maturity");

        var service = new RuntimePackagingProofFederationMaturityService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-packaging-proof-federation-maturity", surface.SurfaceId);
        Assert.Equal(3, surface.PackagingProfiles.Count);
        Assert.Equal(4, surface.ProofLanes.Count);
        Assert.Contains(surface.ClosedCapabilities, capability => capability.CapabilityId == "pack_registry_inventory");

        var proofBundle = Assert.Single(surface.PackagingProfiles, profile => profile.ProfileId == "proof_bundle");
        Assert.Contains("worker_execution_artifact_history", proofBundle.PointerOnlyFamilyIds);

        var approval = Assert.Single(surface.ProofLanes, lane => lane.LaneId == "approval");
        Assert.Contains("approval_proof", approval.SourceHandoffLaneIds);

        var projectionOnly = Assert.Single(surface.FederationLanes, lane => lane.LaneId == "projection_only_surfaces");
        Assert.Equal("projection_only", projectionOnly.Status);
        Assert.Contains(".codex/config.toml", projectionOnly.TruthRefs);
    }

    [Fact]
    public void Build_IsInvalidWhenBoundaryDocumentIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");

        var service = new RuntimePackagingProofFederationMaturityService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths));

        var surface = service.Build();

        Assert.False(surface.IsValid);
        Assert.Contains(surface.Errors, error => error.Contains("runtime-packaging-proof-federation-maturity.md", StringComparison.Ordinal));
    }
}
