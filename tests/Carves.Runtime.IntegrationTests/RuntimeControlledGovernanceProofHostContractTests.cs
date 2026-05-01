using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeControlledGovernanceProofHostContractTests
{
    [Fact]
    public void RuntimeControlledGovernanceProof_InspectAndApiProjectIntegratedProofLanes()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-controlled-governance-proof");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-controlled-governance-proof", "approval");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime governance archive status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Role: compatibility_alias", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-controlled-governance-proof -> runtime-governance-archive-status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Legacy argument: (none)", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-controlled-governance-proof", root.GetProperty("surface_id").GetString());
        Assert.Equal("compatibility_alias", root.GetProperty("surface_role").GetString());
        Assert.Equal("runtime-governance-archive-status", root.GetProperty("successor_surface_id").GetString());
        Assert.Equal("alias_retained", root.GetProperty("retirement_posture").GetString());
        Assert.Equal("approval", root.GetProperty("legacy_argument").GetString());
        Assert.Equal(6, root.GetProperty("legacy_aliases").GetArrayLength());
        Assert.False(root.GetProperty("output_budget").GetProperty("historical_document_bodies_embedded").GetBoolean());
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
