using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class ParallelRuntimeSmokeTests
{
    [Fact]
    public void SessionTickDryRun_DispatchesTwoNonConflictingTasks()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PARALLEL-001", ["tests/Parallel/A"]);
        sandbox.AddSyntheticPendingTask("T-PARALLEL-002", ["tests/Parallel/B"]);

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var tick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick", "--dry-run");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var taskOneJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-001.json")))!.AsObject();
        var taskTwoJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-002.json")))!.AsObject();

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, tick.ExitCode);
        Assert.Contains("T-PARALLEL-001", tick.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-PARALLEL-002", tick.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("pending", taskOneJson["status"]!.GetValue<string>());
        Assert.Equal("pending", taskTwoJson["status"]!.GetValue<string>());
        Assert.Equal("idle", sessionJson["status"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-PARALLEL-001.json")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-PARALLEL-002.json")));
    }

    [Fact]
    public void SessionTick_AllowsUnrelatedTaskToContinueWhileReviewConflictRemainsBlocked()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PARALLEL-101", [".ai/tests/Modules/A"]);
        sandbox.SetTaskMetadata("T-PARALLEL-101", "worker_backend", "null_worker");
        sandbox.AddSyntheticPendingTask("T-PARALLEL-102", [".ai/tests/Modules/B"]);
        sandbox.SetTaskMetadata("T-PARALLEL-102", "worker_backend", "null_worker");
        sandbox.AddSyntheticPendingTask("T-PARALLEL-103", [".ai/tests/Modules/C"]);
        sandbox.SetTaskMetadata("T-PARALLEL-103", "worker_backend", "null_worker");
        sandbox.AddSyntheticPendingTask("T-PARALLEL-104", [".ai/tests/Modules/A/Child"]);
        sandbox.SetTaskMetadata("T-PARALLEL-104", "worker_backend", "null_worker");

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start");
        var firstTick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick");
        var secondTick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick");
        var taskThreeJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-103.json")))!.AsObject();
        var taskFourJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PARALLEL-104.json")))!.AsObject();
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var reviewPending = sessionJson["review_pending_task_ids"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, firstTick.ExitCode);
        Assert.Equal(0, secondTick.ExitCode);
        Assert.Equal("review", taskThreeJson["status"]!.GetValue<string>());
        Assert.Equal("pending", taskFourJson["status"]!.GetValue<string>());
        Assert.Contains("Blocked: T-PARALLEL-104", secondTick.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("review-pending task T-PARALLEL-101", secondTick.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("review_wait", sessionJson["status"]!.GetValue<string>());
        Assert.Contains("T-PARALLEL-103", reviewPending);
    }
}
