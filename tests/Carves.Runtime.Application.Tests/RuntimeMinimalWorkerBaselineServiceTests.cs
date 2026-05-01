using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeMinimalWorkerBaselineServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndKeepsHostGovernanceStronger()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeMinimalWorkerBaselineService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformMinimalWorkerBaselineFile));
        Assert.Equal("runtime-minimal-worker-baseline", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/minimal-worker-baseline.json", surface.PolicyPath);
        Assert.Contains(surface.Policy.ExtractionBoundary.DirectAbsorptions, item => item.Contains("linear worker loop", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.Policy.ExtractionBoundary.DirectAbsorptions, item => item.Contains("subprocess", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.Policy.ExtractionBoundary.RejectedAnchors, item => item.Contains("CARVES Host", StringComparison.Ordinal));
        Assert.Equal(["query", "execute_actions"], surface.Policy.LinearLoop.LoopPhases);
        Assert.Contains(surface.Policy.SubprocessBoundary.RequiredGovernedState, item => item == "task run <task-id>");
        Assert.Equal("weak", surface.Policy.WeakLane.ModelProfileId);
        Assert.Contains(surface.Policy.WeakLane.RequiredStartupSurfaces, item => item == "runtime-agent-bootstrap-packet");
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "host_taskgraph_and_writeback_remain_stronger");
        Assert.Contains(surface.Policy.Qualification.StopConditions, item => item.Contains("truth writeback", StringComparison.OrdinalIgnoreCase));
    }
}
