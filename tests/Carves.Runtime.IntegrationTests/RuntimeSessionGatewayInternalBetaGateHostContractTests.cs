using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeSessionGatewayInternalBetaGateHostContractTests
{
    [Fact]
    public void RuntimeSessionGatewayInternalBetaGate_InspectAndApiProjectBoundedGateTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-session-gateway-internal-beta-gate");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-session-gateway-internal-beta-gate");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Session Gateway internal beta gate", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: internal_beta_gated_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Internal beta doc: docs/session-gateway/internal-beta-gate.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Successful proof packet: docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-site-scope-repair.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Private alpha handoff posture: private_alpha_deliverable_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Repeatability posture: repeatable_private_alpha_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Blocked claims:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-session-gateway-internal-beta-gate", root.GetProperty("surface_id").GetString());
        Assert.Equal("internal_beta_gated_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("private_alpha_deliverable_ready", root.GetProperty("private_alpha_handoff_posture").GetString());
        Assert.Equal("repeatable_private_alpha_ready", root.GetProperty("repeatability_posture").GetString());
        Assert.Equal("docs/session-gateway/internal-beta-gate.md", root.GetProperty("internal_beta_gate_path").GetString());
        Assert.Equal("docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-site-scope-repair.md", root.GetProperty("successful_proof_packet_path").GetString());
        Assert.Equal(".ai/runtime/internal-operator-proof/2026-04-06-carves-site-session-gateway-scope-repair.json", root.GetProperty("successful_proof_evidence_path").GetString());
        Assert.Equal("repo_local_proof", root.GetProperty("operator_proof_contract").GetProperty("current_proof_source").GetString());
        Assert.Equal("WAITING_OPERATOR_SETUP", root.GetProperty("operator_proof_contract").GetProperty("current_operator_state").GetString());
        Assert.True(root.GetProperty("included_scope").GetArrayLength() >= 4);
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
