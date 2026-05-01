using System.Reflection;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class ProductShellTests
{
    [Fact]
    public void ResidentHost_RunsLoopAndPausesManagedRepoOnExit()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-HOST-LOOP");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--dry-run", "--cycles", "1", "--interval-ms", "0");
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-HOST-LOOP.json");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("Runtime Host started", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Session loop running...", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"paused\"", sessionJson, StringComparison.Ordinal);
        Assert.True(File.Exists(workerArtifactPath));
    }

    [Fact]
    public void ResidentHost_DoesNotRestartStoppedRuntime()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-host-stopped");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "runtime", "start", "repo-host-stopped", "--dry-run");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "runtime", "stop", "repo-host-stopped", "Stopped", "by", "operator");
        sandbox.AddSyntheticPendingTask("T-HOST-STOPPED");

        var services = RuntimeComposition.Create(sandbox.RootPath);
        var host = new ResidentRuntimeHostService(services);
        var runRepoCycle = typeof(ResidentRuntimeHostService).GetMethod("RunRepoCycle", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RunRepoCycle method was not found.");
        var lines = (IReadOnlyList<string>)runRepoCycle.Invoke(host, ["repo-host-stopped", true, null])!;
        var output = string.Join('\n', lines);
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-HOST-STOPPED.json");

        Assert.Contains("[repo-host-stopped] Skipped stopped runtime instance; explicit runtime start is required before host scheduling can advance it.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[repo-host-stopped] Started runtime instance.", output, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"stopped\"", sessionJson, StringComparison.Ordinal);
        Assert.False(File.Exists(workerArtifactPath));
    }

    [Fact]
    public void Console_ScriptedMenuRoutesAndSafelyExits()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "console", "--script", "1,3,8,0");
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("CARVES Operator Console", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("1. Platform status", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Paused session", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"paused\"", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscussContext_EmitsDelegationWarningAndTelemetryForDirtySourceWithoutRecentDelegation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        InitializeGitRepo(sandbox.RootPath);
        File.AppendAllText(Path.Combine(sandbox.RootPath, "src", "CARVES.Runtime.Host", "Program.cs"), Environment.NewLine + "// delegation drift test");

        var discuss = ProgramHarness.Run("--repo-root", sandbox.RootPath, "discuss", "context");
        var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "events", "--kind", "DelegationBypassDetected");

        Assert.Equal(0, discuss.ExitCode);
        Assert.Contains("\"delegation\"", discuss.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Protected source areas are dirty and no recent delegation telemetry was observed.", discuss.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, events.ExitCode);
        Assert.Contains("DelegationBypassDetected", events.StandardOutput, StringComparison.Ordinal);
    }

    private static void InitializeGitRepo(string rootPath)
    {
        GitTestHarness.InitializeRepository(rootPath);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        GitTestHarness.Run(workingDirectory, arguments);
    }
}
