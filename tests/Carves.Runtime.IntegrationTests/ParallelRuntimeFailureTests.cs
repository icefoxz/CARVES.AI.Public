using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class ParallelRuntimeFailureTests
{
    [Fact]
    public void SessionTick_PersistsFailureArtifactsWhenOneParallelTaskFails()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-PARALLEL-SUCCESS",
            [".ai/tests/Parallel/Success"],
            [[ "dotnet", "--version" ]]);
        sandbox.SetTaskMetadata("T-PARALLEL-SUCCESS", "worker_backend", "null_worker");
        sandbox.AddSyntheticPendingTask(
            "T-PARALLEL-FAIL",
            ["tests/Parallel/Failure"],
            [[ "definitely-not-a-real-executable-carves" ]]);
        sandbox.SetTaskMetadata("T-PARALLEL-FAIL", "worker_backend", "codex_cli");

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start");
        var tick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick");
        var successTaskJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-SUCCESS.json")))!.AsObject();
        var failureTaskJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-FAIL.json")))!.AsObject();
        var failureSnapshotJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "last_failure.json")))!.AsObject();
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var failureHistory = Directory.EnumerateFiles(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "runtime-failures"), "*.json").ToArray();

        Assert.Equal(0, start.ExitCode);
        Assert.NotEqual(0, tick.ExitCode);
        Assert.Equal("review", successTaskJson["status"]!.GetValue<string>());
        Assert.Equal("pending", failureTaskJson["status"]!.GetValue<string>());
        Assert.Equal("worker_execution_failure", failureSnapshotJson["failure_type"]!.GetValue<string>());
        Assert.Equal("paused", sessionJson["status"]!.GetValue<string>());
        Assert.NotEmpty(failureHistory);
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-PARALLEL-SUCCESS.json")));
    }
}
