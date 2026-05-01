using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class ClaudeWorkerHostContractTests
{
    [Fact]
    public void RuntimeClaudeWorkerQualification_ProjectsQualifiedAndClosedLanes()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-claude-worker-qualification");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-claude-worker-qualification");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Claude worker qualification", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("review_summary", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("patch_draft", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("materialized patch/result submission remains out of scope", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-claude-worker-qualification", root.GetProperty("surface_id").GetString());
        var lanes = root.GetProperty("current_policy").GetProperty("lanes").EnumerateArray().ToArray();
        Assert.Contains(lanes, item => item.GetProperty("routing_intent").GetString() == "review_summary" && item.GetProperty("allowed").GetBoolean());
        Assert.Contains(lanes, item => item.GetProperty("routing_intent").GetString() == "patch_draft" && !item.GetProperty("allowed").GetBoolean());
    }

    private static ProgramHarness.ProgramRunResult RunProgram(params string[] arguments)
    {
        return ProgramHarness.Run(arguments);
    }
}
