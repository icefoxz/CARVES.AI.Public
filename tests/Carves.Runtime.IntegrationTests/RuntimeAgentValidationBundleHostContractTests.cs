using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeAgentValidationBundleHostContractTests
{
    [Fact]
    public void RuntimeAgentValidationBundle_InspectAndApiProjectBoundedValidationGate()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-validation-bundle");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-validation-bundle");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime agent validation bundle", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Boundary document: docs/runtime/runtime-agent-governed-validation-bundle-test-hardening-contract.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Guide path: docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_v1_validation_bundle_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validation ownership: runtime_owned_v1_validation_bundle", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: gateway_and_governed_entry_lane", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: governed_dev_lane", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: host_lifecycle_and_entry_honesty_lane", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("docs/runtime/runtime-host-lifecycle-proof-gate.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/beta/host-lifecycle-proof-lane.ps1", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("note: This lane excludes the known mutation-forwarding timeout drift from the default bundle.", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-agent-validation-bundle", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_v1_validation_bundle_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_owned_v1_validation_bundle", root.GetProperty("validation_ownership").GetString());
        Assert.Equal("docs/runtime/runtime-agent-governed-validation-bundle-test-hardening-contract.md", root.GetProperty("boundary_document_path").GetString());
        Assert.Equal("docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", root.GetProperty("guide_path").GetString());
        Assert.Equal(6, root.GetProperty("lanes").GetArrayLength());
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item => item.GetProperty("lane_id").GetString() == "contract_and_read_model_lane");
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item => item.GetProperty("lane_id").GetString() == "attach_and_first_run_lane");
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item => item.GetProperty("lane_id").GetString() == "failure_and_recovery_lane");
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item =>
            item.GetProperty("lane_id").GetString() == "governed_dev_lane"
            && item.GetProperty("validation_commands").EnumerateArray().Any(command => command.GetString()!.Contains("HostStatusJson_WithHost_ProjectsSelectedLoopCapabilityReadiness", StringComparison.Ordinal)));
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item =>
            item.GetProperty("lane_id").GetString() == "governed_dev_lane"
            && item.GetProperty("validation_commands").EnumerateArray().Any(command => command.GetString()!.Contains("TaskInspectWithRuns_WithHost_ProjectsCandidateReadBeforeDryRun", StringComparison.Ordinal)));
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item =>
            item.GetProperty("lane_id").GetString() == "governed_dev_lane"
            && item.GetProperty("validation_commands").EnumerateArray().Any(command => command.GetString()!.Contains("TaskIngestResult_WithoutHost_RequiresExplicitHostEnsureAndDoesNotMutateTruth", StringComparison.Ordinal)));
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item =>
            item.GetProperty("lane_id").GetString() == "governed_dev_lane"
            && item.GetProperty("validation_commands").EnumerateArray().Any(command => command.GetString()!.Contains("SyncState_WithHost_RoutesThroughResidentHost", StringComparison.Ordinal)));
        Assert.Contains(root.GetProperty("lanes").EnumerateArray(), item =>
            item.GetProperty("lane_id").GetString() == "host_lifecycle_and_entry_honesty_lane"
            && item.GetProperty("validation_commands").EnumerateArray().Any(command => command.GetString()!.Contains("host-lifecycle-proof-lane.ps1", StringComparison.Ordinal)));
        Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString()!.Contains("whole-suite perfection", StringComparison.Ordinal));
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
