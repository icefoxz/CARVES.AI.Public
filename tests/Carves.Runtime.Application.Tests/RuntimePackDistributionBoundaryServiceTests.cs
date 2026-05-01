using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackDistributionBoundaryServiceTests
{
    [Fact]
    public void BuildSurface_ProjectsLocalCapabilitiesAndClosedFutureBoundary()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackDistributionBoundaryService(artifactRepository);

        var surface = service.BuildSurface();

        Assert.Equal("runtime-pack-distribution-boundary", surface.SurfaceId);
        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_local_pack_selection");
        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_local_pack_history_and_rollback");
        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_local_pack_audit_evidence");
        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_pack_execution_audit");
        Assert.Contains(surface.ClosedFutureCapabilities, capability => capability.CapabilityId == "pack_registry_sync");
        Assert.Contains(surface.ClosedFutureCapabilities, capability => capability.CapabilityId == "pack_rollout_assignment");
        Assert.Contains("Registry and rollout remain closed future lines", surface.Summary, StringComparison.Ordinal);
        Assert.Equal(0, surface.CurrentTruth.SelectionHistoryCount);
    }
}
