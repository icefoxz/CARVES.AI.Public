using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentGovernanceKernelServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndPreservesKeyFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeAgentGovernanceKernelService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformAgentGovernanceKernelFile));
        Assert.Equal("runtime-agent-governance-kernel", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/agent-governance-kernel.json", surface.PolicyPath);
        Assert.Contains(surface.Policy.PathFamilies, item => item.FamilyId == "bootstrap_anchor" && item.CommitClass == "requires_governed_card_first");
        Assert.Contains(surface.Policy.PathFamilies, item => item.FamilyId == "checkpoint_docs" && item.LifecycleClass == "OutsideAiLifecycle");
        Assert.Contains(surface.Policy.MixedRoots, item => item.RootPath == ".carves-platform/runtime-state/");
        Assert.Equal("UnclassifiedPath", surface.Policy.UnclassifiedDefault.LifecycleClass);
        Assert.Contains("docs/runtime/runtime-artifact-boundary-normalization.md", surface.Policy.GovernanceBoundaryDocPatterns, StringComparer.Ordinal);
    }
}
