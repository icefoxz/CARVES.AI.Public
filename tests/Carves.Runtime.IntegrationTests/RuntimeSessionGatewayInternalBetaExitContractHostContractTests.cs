using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeSessionGatewayInternalBetaExitContractHostContractTests
{
    [Fact]
    public void RuntimeSessionGatewayInternalBetaExitContract_InspectAndApiProjectWeightedEvidenceTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-session-gateway-internal-beta-exit-contract");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-session-gateway-internal-beta-exit-contract");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Session Gateway internal beta exit contract", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: internal_beta_exit_contract_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Exit contract doc: docs/session-gateway/internal-beta-exit-contract.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Internal beta gate posture: internal_beta_gated_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("First-run packet posture: first_run_packet_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("sample: carves_site_first_run repo=CARVES.Site", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("sample: carves_unity_first_run repo=CARVES.Unity", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("sample: carves_dev_first_run repo=CARVES.DEV", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Blocked claims:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-session-gateway-internal-beta-exit-contract", root.GetProperty("surface_id").GetString());
        Assert.Equal("internal_beta_exit_contract_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("internal_beta_gated_ready", root.GetProperty("internal_beta_gate_posture").GetString());
        Assert.Equal("first_run_packet_ready", root.GetProperty("first_run_packet_posture").GetString());
        Assert.Equal("docs/session-gateway/internal-beta-exit-contract.md", root.GetProperty("exit_contract_path").GetString());
        Assert.Equal("repo_local_proof", root.GetProperty("operator_proof_contract").GetProperty("current_proof_source").GetString());
        Assert.Equal("WAITING_OPERATOR_SETUP", root.GetProperty("operator_proof_contract").GetProperty("current_operator_state").GetString());

        var samples = root.GetProperty("samples").EnumerateArray().ToArray();
        Assert.Equal(3, samples.Length);
        Assert.Contains(samples, sample =>
            sample.GetProperty("repo_id").GetString() == "CARVES.Site"
            && sample.GetProperty("counts_as_representative_evidence").GetBoolean()
            && sample.GetProperty("evidence_weight").GetString() == "representative_attached_repo");
        Assert.Contains(samples, sample =>
            sample.GetProperty("repo_id").GetString() == "CARVES.Unity"
            && sample.GetProperty("counts_as_representative_evidence").GetBoolean()
            && sample.GetProperty("evidence_weight").GetString() == "representative_current_shape");
        Assert.Contains(samples, sample =>
            sample.GetProperty("repo_id").GetString() == "CARVES.DEV"
            && !sample.GetProperty("counts_as_representative_evidence").GetBoolean()
            && sample.GetProperty("evidence_weight").GetString() == "robustness_only");
        Assert.True(root.GetProperty("representative_evidence_basis").GetArrayLength() >= 3);
        Assert.True(root.GetProperty("blocked_claims").GetArrayLength() >= 4);
        Assert.True(root.GetProperty("entry_commands").GetArrayLength() >= 3);
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
