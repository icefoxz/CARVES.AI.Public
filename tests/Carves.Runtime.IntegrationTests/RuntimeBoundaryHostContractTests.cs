using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeBoundaryHostContractTests
{
    [Fact]
    public void RuntimeCoreAdapterBoundary_ProjectsFamiliesAndNeutralContracts()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-core-adapter-boundary");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-core-adapter-boundary");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime core/adapter boundary", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("platform_provider_definition_truth", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_packet", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-core-adapter-boundary", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "agent_bootstrap_projection");
        Assert.Contains(root.GetProperty("families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "platform_provider_live_state");
        Assert.Contains(root.GetProperty("core_contracts").EnumerateArray(), item => item.GetProperty("contract_id").GetString() == "taskgraph_node");
        Assert.Contains(root.GetProperty("core_contracts").EnumerateArray(), item => item.GetProperty("contract_id").GetString() == "execution_run_report");
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
            var exitCode = Program.Main(args);
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
