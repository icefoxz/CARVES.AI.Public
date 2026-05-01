using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeDefaultWorkflowProofHostContractTests
{
    [Fact]
    public void InspectAndApiRuntimeDefaultWorkflowProof_ProjectShortDefaultPathAndCurrentBlockers()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-default-workflow-proof");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-default-workflow-proof");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime default workflow proof", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Workflow proof complete: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves agent start --json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves agent context --json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current runtime blockers:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("does not claim the current repo or target is ready", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-default-workflow-proof", root.GetProperty("surface_id").GetString());
        Assert.True(root.GetProperty("workflow_proof_complete").GetBoolean());
        Assert.Equal(0, root.GetProperty("structural_gaps").GetArrayLength());
        Assert.Equal(2, root.GetProperty("default_first_thread_command_count").GetInt32());
        Assert.Equal(2, root.GetProperty("default_warm_reorientation_command_count").GetInt32());
        Assert.Equal(0, root.GetProperty("post_initialization_markdown_tokens").GetInt32());
        Assert.True(root.GetProperty("short_context_ready").GetBoolean());
        Assert.True(root.GetProperty("markdown_read_path_within_budget").GetBoolean());
        Assert.True(root.GetProperty("governance_surface_coverage_complete").GetBoolean());
        Assert.True(root.GetProperty("resource_pack_covers_default_commands").GetBoolean());
        Assert.Contains(root.GetProperty("default_path").EnumerateArray(), step =>
            step.GetProperty("step_id").GetString() == "first_thread_start"
            && step.GetProperty("command").GetString() == "carves agent start --json");
        Assert.Contains(root.GetProperty("warm_path").EnumerateArray(), step =>
            step.GetProperty("step_id").GetString() == "warm_reorientation"
            && step.GetProperty("command").GetString() == "carves agent context --json");
        Assert.Contains(root.GetProperty("optional_proof_and_troubleshooting_path").EnumerateArray(), step =>
            step.GetProperty("surface_id").GetString() == "runtime-default-workflow-proof");
        Assert.Contains(root.GetProperty("checks").EnumerateArray(), check =>
            check.GetProperty("check_id").GetString() == "troubleshooting_not_default_path"
            && check.GetProperty("passed").GetBoolean());
    }
}
