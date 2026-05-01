using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeFirstRunOperatorPacketHostContractTests
{
    [Fact]
    public void RuntimeFirstRunOperatorPacket_InspectAndApiProjectBoundedEntryTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-first-run-operator-packet");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-first-run-operator-packet");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime first-run operator packet", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: first_run_packet_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Packet doc: docs/runtime/runtime-first-run-operator-packet.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime document root mode: repo_local", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Internal beta gate posture:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current proof source: repo_local_proof", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Minimum onboarding reads:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- onboarding-read: README.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- onboarding-read: AGENTS.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Blocked claims:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-first-run-operator-packet", root.GetProperty("surface_id").GetString());
        Assert.Equal("first_run_packet_ready", root.GetProperty("overall_posture").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("internal_beta_gate_posture").GetString()));
        Assert.Equal("repo_local", root.GetProperty("runtime_document_root_mode").GetString());
        Assert.Equal("repo_local_proof", root.GetProperty("current_proof_source").GetString());
        Assert.Equal("WAITING_OPERATOR_SETUP", root.GetProperty("current_operator_state").GetString());
        Assert.Equal("docs/runtime/runtime-first-run-operator-packet.md", root.GetProperty("packet_path").GetString());
        Assert.Equal("docs/runtime/runtime-trusted-bootstrap-truth-schema.md", root.GetProperty("trusted_bootstrap_truth_path").GetString());
        Assert.Equal("docs/runtime/runtime-onboarding-acceleration-contract.md", root.GetProperty("onboarding_acceleration_contract_path").GetString());
        Assert.True(root.GetProperty("project_identification").GetArrayLength() >= 3);
        Assert.True(root.GetProperty("bootstrap_truth_families").GetArrayLength() >= 5);
        Assert.True(root.GetProperty("required_operator_actions").GetArrayLength() >= 4);
        Assert.True(root.GetProperty("allowed_ai_assistance").GetArrayLength() >= 3);
        Assert.True(root.GetProperty("entry_commands").GetArrayLength() >= 4);
        Assert.Equal("README.md", root.GetProperty("minimum_onboarding_reads")[0].GetString());
        Assert.Equal("AGENTS.md", root.GetProperty("minimum_onboarding_reads")[1].GetString());
        Assert.True(root.GetProperty("minimum_onboarding_next_steps").GetArrayLength() >= 3);
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
