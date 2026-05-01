using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeDurableExecutionSemanticsServiceTests
{
    [Fact]
    public void Build_CreatesPolicyTruthAndRejectsTaskGraphReplacement()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeDurableExecutionSemanticsService(workspace.RootPath, workspace.Paths);

        var surface = service.Build();

        Assert.True(File.Exists(workspace.Paths.PlatformDurableExecutionSemanticsFile));
        Assert.Equal("runtime-durable-execution-semantics", surface.SurfaceId);
        Assert.Equal(".carves-platform/policies/durable-execution-semantics.json", surface.PolicyPath);
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "checkpoint_semantics");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "resume_semantics");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "human_interrupt_points");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "state_inspection_surfaces");
        Assert.Contains(surface.Policy.ConcernFamilies, item => item.FamilyId == "execution_memory_separation");
        Assert.Contains(surface.Policy.ExtractionBoundary.RejectedAnchors, item => item.Contains("TaskGraph", StringComparison.Ordinal));
        Assert.Contains(surface.Policy.BoundaryRules, item => item.RuleId == "taskgraph_replacement_is_rejected");
        Assert.Contains(surface.Policy.GovernedReadPaths, item => item.PathId == "resume_gate_and_runtime_controls");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "taskgraph_replacement" && item.Readiness == "rejected");
        Assert.Contains(surface.Policy.ReadinessMap, item => item.SemanticId == "multi_worker_durable_orchestration" && item.Readiness == "deferred_to_existing_lineage");
        Assert.Contains(surface.Policy.Qualification.StopConditions, item => item.Contains("TaskGraph", StringComparison.Ordinal));
    }
}
