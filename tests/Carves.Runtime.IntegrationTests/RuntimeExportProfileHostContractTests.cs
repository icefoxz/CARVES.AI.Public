using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeExportProfileHostContractTests
{
    [Fact]
    public void RuntimeExportProfiles_InspectAndApiProjectResolvedProfiles()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-export-profiles");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-export-profiles", "proof_bundle");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime export profiles", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("source_review", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("proof_bundle", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_state_package", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("discipline: valid=True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pointer_only families: worker_execution_artifact_history", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-export-profiles", root.GetProperty("surface_id").GetString());
        var profile = Assert.Single(root.GetProperty("profiles").EnumerateArray());
        Assert.Equal("proof_bundle", profile.GetProperty("profile_id").GetString());
        Assert.True(profile.GetProperty("discipline").GetProperty("is_valid").GetBoolean());
        Assert.Contains(profile.GetProperty("discipline").GetProperty("pointer_only_family_ids").EnumerateArray(), item =>
            item.GetString() == "worker_execution_artifact_history");
        Assert.Contains(profile.GetProperty("included_families").EnumerateArray(), item =>
            item.GetProperty("family_id").GetString() == "worker_execution_artifact_history"
            && item.GetProperty("packaging_mode").GetString() == "pointer_only");
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
