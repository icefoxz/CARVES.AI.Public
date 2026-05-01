using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeValidationLabProofHandoffHostContractTests
{
    [Fact]
    public void RuntimeValidationLabProofHandoff_InspectAndApiProjectBoundedProofLanes()
    {
        var repoRoot = ResolveRepoRoot();

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", "runtime-validationlab-proof-handoff");
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", "runtime-validationlab-proof-handoff", "approval_proof");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime governance archive status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Role: compatibility_alias", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-validationlab-proof-handoff -> runtime-governance-archive-status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Legacy argument: (none)", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-validationlab-proof-handoff", root.GetProperty("surface_id").GetString());
        Assert.Equal("compatibility_alias", root.GetProperty("surface_role").GetString());
        Assert.Equal("runtime-governance-archive-status", root.GetProperty("successor_surface_id").GetString());
        Assert.Equal("alias_retained", root.GetProperty("retirement_posture").GetString());
        Assert.Equal("approval_proof", root.GetProperty("legacy_argument").GetString());
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

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
