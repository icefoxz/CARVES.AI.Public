using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeAgentDeliveryReadinessHostContractTests
{
    [Fact]
    public void RuntimeAgentDeliveryReadiness_InspectAndApiProjectBoundedStage6DeliveryLane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-delivery-readiness");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-delivery-readiness");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime agent delivery readiness", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Boundary document: docs/runtime/runtime-agent-governed-packaging-closure-delivery-readiness-contract.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_delivery_readiness_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Entry lane: resident_host_attach_first_run_validation_bundle", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- truth-file: .ai/runtime.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- related-surface: runtime-agent-validation-bundle", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-agent-delivery-readiness", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_delivery_readiness_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_owned_source_tree_trial_entry", root.GetProperty("delivery_ownership").GetString());
        Assert.Equal("resident_host_attach_first_run_validation_bundle", root.GetProperty("entry_lane_id").GetString());
        Assert.Equal("scripts/carves-host.ps1", root.GetProperty("trial_wrapper_path").GetString());
        Assert.Contains(root.GetProperty("runtime_truth_files").EnumerateArray(), item => item.GetString() == ".ai/runtime.json");
        Assert.Contains(root.GetProperty("blocked_claims").EnumerateArray(), item => item.GetString() == "installer_owned_bootstrap_truth");
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
