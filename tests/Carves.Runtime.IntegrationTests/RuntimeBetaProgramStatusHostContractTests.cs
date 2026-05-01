using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeBetaProgramStatusHostContractTests
{
    [Fact]
    public void RuntimeBetaProgramStatus_InspectAndApiProjectMergedRuntimeOwnedBetaContract()
    {
        var repoRoot = ResolveRepoRoot();

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", "runtime-beta-program-status");
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", "runtime-beta-program-status");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime beta program status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: runtime_beta_program_status_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Consolidated surface count: 5", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- merged-surface: runtime-beta-upgrade-readiness", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- phase: P5 hosted_agent_broker (contract_ready)", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-beta-program-status", root.GetProperty("surface_id").GetString());
        Assert.Equal("runtime_beta_program_status_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_control_kernel", root.GetProperty("truth_owner").GetString());
        Assert.Equal("runtime_owned_beta_program_status", root.GetProperty("contract_ownership").GetString());
        Assert.Equal(5, root.GetProperty("consolidated_surface_count").GetInt32());

        var mergedSurfaces = root.GetProperty("consolidated_from_surface_ids").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("runtime-beta-upgrade-readiness", mergedSurfaces);
        Assert.Contains("runtime-beta-hosted-broker-readiness", mergedSurfaces);

        var phases = root.GetProperty("phases").EnumerateArray().ToArray();
        Assert.Equal(5, phases.Length);
        Assert.Contains(phases, phase =>
            phase.GetProperty("phase_id").GetString() == "P1"
            && phase.GetProperty("overall_posture").GetString() == "contract_ready");
        Assert.Contains(phases, phase =>
            phase.GetProperty("phase_id").GetString() == "P3"
            && phase.GetProperty("supporting_doc_path").GetString() == "docs/runtime/runtime-beta-bootstrap-readiness.md");
        Assert.Contains(phases, phase =>
            phase.GetProperty("phase_id").GetString() == "P5"
            && phase.GetProperty("supporting_doc_path").GetString() == "docs/runtime/runtime-beta-hosted-broker-readiness.md");
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

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
