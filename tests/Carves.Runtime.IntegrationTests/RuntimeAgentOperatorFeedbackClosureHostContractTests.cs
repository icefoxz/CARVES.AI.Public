using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeAgentOperatorFeedbackClosureHostContractTests
{
    [Fact]
    public void RuntimeAgentOperatorFeedbackClosure_InspectAndApiProjectBoundedFeedbackBundles()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-operator-feedback-closure");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-operator-feedback-closure");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime agent operator feedback closure", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_operator_feedback_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- bundle: host_start_and_attach", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- bundle: delivery_and_validation_readback", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("commands: carves maintain repair, carves inspect runtime-agent-failure-recovery-closure, carves status", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-agent-operator-feedback-closure", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_operator_feedback_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_owned_operator_guidance_projection", root.GetProperty("feedback_ownership").GetString());
        Assert.Equal(5, root.GetProperty("feedback_bundles").GetArrayLength());
        Assert.Contains(root.GetProperty("feedback_bundles").EnumerateArray(), item => item.GetProperty("bundle_id").GetString() == "dispatchable_run");
        Assert.Contains(root.GetProperty("feedback_bundles").EnumerateArray(), item => item.GetProperty("bundle_id").GetString() == "repair_and_recovery");
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
