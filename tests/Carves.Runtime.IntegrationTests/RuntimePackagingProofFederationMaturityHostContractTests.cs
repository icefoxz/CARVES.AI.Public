using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimePackagingProofFederationMaturityHostContractTests
{
    [Fact]
    public void RuntimePackagingProofFederationMaturity_InspectAndApiProjectBoundedMaturity()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-packaging-proof-federation-maturity");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-packaging-proof-federation-maturity");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime governance archive status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Role: compatibility_alias", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-packaging-proof-federation-maturity -> runtime-governance-archive-status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("historical_document_bodies_embedded=False", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-packaging-proof-federation-maturity", root.GetProperty("surface_id").GetString());
        Assert.Equal("compatibility_alias", root.GetProperty("surface_role").GetString());
        Assert.Equal("runtime-governance-archive-status", root.GetProperty("successor_surface_id").GetString());
        Assert.Equal("alias_retained", root.GetProperty("retirement_posture").GetString());
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
