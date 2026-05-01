using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackDistributionBoundaryExtendedTests
{
    [Fact]
    public void BuildSurface_ProjectsTaskExplainabilityAndSwitchPolicyCapabilities()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackDistributionBoundaryService(artifactRepository);

        var surface = service.BuildSurface();

        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_pack_task_explainability");
        Assert.Contains(surface.LocalCapabilities, capability => capability.CapabilityId == "runtime_local_pack_switch_policy");
    }
}
