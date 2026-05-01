using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeGuidedPlanningBoundaryHostContractTests
{
    [Fact]
    public void RuntimeGuidedPlanningBoundary_InspectAndApiProjectOneRuntimeOwnedTruthLane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-guided-planning-boundary");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-guided-planning-boundary");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime guided planning boundary", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Boundary document: docs/runtime/runtime-guided-planning-intent-stabilizer-graph-boundary.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_guided_planning_boundary_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Focus field: focus_card_id", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Preferred interactive projection: downstream_web_graph_canvas", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- planning-object: grounded_card | truth-role=host_routed_writeback_bridge | writeback=create_card_draft_then_approve_then_plan_card_persist_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- blocked-claim: mermaid_as_canonical_truth", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-guided-planning-boundary", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_guided_planning_boundary_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_control_kernel", root.GetProperty("truth_owner").GetString());
        Assert.Equal("carves_operator", root.GetProperty("interactive_shell_owner").GetString());
        Assert.Equal("focus_card_id", root.GetProperty("focus_field").GetString());
        Assert.Equal("downstream_web_graph_canvas", root.GetProperty("preferred_interactive_projection").GetString());
        Assert.Contains(root.GetProperty("auxiliary_graph_projections").EnumerateArray(), item => item.GetString() == "mermaid_export");
        Assert.Contains(root.GetProperty("planning_objects").EnumerateArray(), item =>
            item.GetProperty("object_id").GetString() == "candidate_card"
            && item.GetProperty("writeback_eligibility").GetString() == "not_official_card_truth_until_grounded");
        Assert.Contains(root.GetProperty("blocked_claims").EnumerateArray(), item => item.GetString() == "unity_as_v1_guided_planning_shell");
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Host.Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
