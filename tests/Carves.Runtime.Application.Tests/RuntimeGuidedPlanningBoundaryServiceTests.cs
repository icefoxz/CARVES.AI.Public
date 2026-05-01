using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeGuidedPlanningBoundaryServiceTests
{
    [Fact]
    public void Build_ProjectsGuidedPlanningTruthObjectsAndWritebackBoundary()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-guided-planning-intent-stabilizer-graph-boundary.md", "# guided planning");
        workspace.WriteFile("docs/runtime/workbench-v1-scope-and-boundary.md", "# workbench");
        workspace.WriteFile("docs/session-gateway/session-gateway-v1.md", "# session gateway");
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");
        workspace.WriteFile("docs/guides/HOST_AND_PROVIDER_QUICKSTART.md", "# quickstart");

        var service = new RuntimeGuidedPlanningBoundaryService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-guided-planning-boundary", surface.SurfaceId);
        Assert.Equal("bounded_guided_planning_boundary_ready", surface.OverallPosture);
        Assert.Equal("runtime_control_kernel", surface.TruthOwner);
        Assert.Equal("carves_operator", surface.InteractiveShellOwner);
        Assert.Equal("focus_card_id", surface.FocusField);
        Assert.Equal("downstream_web_graph_canvas", surface.PreferredInteractiveProjection);
        Assert.Contains("mermaid_export", surface.AuxiliaryGraphProjections, StringComparer.Ordinal);
        Assert.Contains("wobbling", surface.PlanningPostures, StringComparer.Ordinal);
        Assert.Contains("approved", surface.OfficialLifecycle, StringComparer.Ordinal);
        Assert.Contains(surface.PlanningObjects, item => item.ObjectId == "candidate_card" && item.WritebackEligibility == "not_official_card_truth_until_grounded");
        Assert.Contains(surface.PlanningObjects, item => item.ObjectId == "grounded_card" && item.WritebackEligibility == "create_card_draft_then_approve_then_plan_card_persist_only");
        Assert.Contains(surface.WritebackCommands, item => item.Contains("create-card-draft", StringComparison.Ordinal));
        Assert.Contains(surface.WritebackCommands, item => item.Contains("plan-card", StringComparison.Ordinal) && item.Contains("--persist", StringComparison.Ordinal));
        Assert.Contains(surface.BlockedClaims, item => item == "mermaid_as_canonical_truth");
        Assert.Contains(surface.BlockedClaims, item => item == "unity_as_v1_guided_planning_shell");
        Assert.Contains(surface.NonClaims, item => item.Contains("second planner", StringComparison.Ordinal));
    }
}
