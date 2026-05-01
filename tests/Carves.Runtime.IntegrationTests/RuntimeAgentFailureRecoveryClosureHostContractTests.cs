using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeAgentFailureRecoveryClosureHostContractTests
{
    [Fact]
    public void RuntimeAgentFailureRecoveryClosure_InspectAndApiProjectBoundedFailureAndHandoffTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-failure-recovery-closure");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-failure-recovery-closure");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime agent failure classification and recovery closure", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_failure_recovery_closure_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Read-only visibility commands:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Operator handoff commands:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- failure-class: delegated_lifecycle_drift", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- failure-class: semantic_task_failure", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- blocked-behavior: silent retry loops", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-agent-failure-recovery-closure", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_failure_recovery_closure_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("runtime_control_kernel", root.GetProperty("truth_owner").GetString());
        Assert.Equal("runtime_owned_failure_classification_and_operator_handoff", root.GetProperty("recovery_ownership").GetString());
        Assert.Contains(root.GetProperty("retryable_failure_kinds").EnumerateArray(), item => item.GetString() == "transient_infra");
        Assert.Contains(root.GetProperty("recovery_outcomes").EnumerateArray(), item => item.GetString() == "rebuild_worktree");
        Assert.Contains(root.GetProperty("failure_classes").EnumerateArray(), item => item.GetProperty("failure_class_id").GetString() == "delegated_lifecycle_drift");
        Assert.Contains(root.GetProperty("failure_classes").EnumerateArray(), item => item.GetProperty("failure_class_id").GetString() == "operator_stop_or_unknown_failure");
        Assert.True(root.GetProperty("read_only_visibility_commands").GetArrayLength() >= 4);
        Assert.True(root.GetProperty("operator_handoff_commands").GetArrayLength() >= 3);
        Assert.Contains(root.GetProperty("blocked_behaviors").EnumerateArray(), item => item.GetString() == "silent retry loops");
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
