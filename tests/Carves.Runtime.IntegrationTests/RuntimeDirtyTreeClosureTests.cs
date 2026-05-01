namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeDirtyTreeClosureTests
{
    [Fact]
    public void SyncState_WithNoProjectionDrift_DoesNotRewriteMirrorsOrHealthSidecar()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var first = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");

        Assert.True(first.ExitCode == 0, first.CombinedOutput);

        var projectedPaths = new[]
        {
            Path.Combine(sandbox.RootPath, ".ai", "tasks", "graph.json"),
            Path.Combine(sandbox.RootPath, ".ai", "CURRENT_TASK.md"),
            Path.Combine(sandbox.RootPath, ".ai", "STATE.md"),
            Path.Combine(sandbox.RootPath, ".ai", "TASK_QUEUE.md"),
        };
        var beforeProjection = projectedPaths.ToDictionary(static path => path, File.ReadAllText);
        var healthPath = Path.Combine(sandbox.RootPath, ".carves-platform", "runtime-state", "markdown_projection_health.json");
        var healthExistedBefore = File.Exists(healthPath);
        var healthBefore = healthExistedBefore ? File.ReadAllText(healthPath) : string.Empty;

        var second = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");

        Assert.True(second.ExitCode == 0, second.CombinedOutput);
        foreach (var path in projectedPaths)
        {
            Assert.Equal(beforeProjection[path], File.ReadAllText(path));
        }

        Assert.Equal(healthExistedBefore, File.Exists(healthPath));
        if (healthExistedBefore)
        {
            Assert.Equal(healthBefore, File.ReadAllText(healthPath));
        }
    }

    [Fact]
    public void RunNextDryRun_WithNoSessionAndNoReadyWork_DoesNotMaterializeLiveStateOrRewriteMirrors()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var currentTaskPath = Path.Combine(sandbox.RootPath, ".ai", "CURRENT_TASK.md");
        var statePath = Path.Combine(sandbox.RootPath, ".ai", "STATE.md");
        var taskQueuePath = Path.Combine(sandbox.RootPath, ".ai", "TASK_QUEUE.md");
        var beforeCurrentTask = File.ReadAllText(currentTaskPath);
        var beforeState = File.ReadAllText(statePath);
        var beforeTaskQueue = File.ReadAllText(taskQueuePath);

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")));
        Assert.Equal(beforeCurrentTask, File.ReadAllText(currentTaskPath));
        Assert.Equal(beforeState, File.ReadAllText(statePath));
        Assert.Equal(beforeTaskQueue, File.ReadAllText(taskQueuePath));
    }

    [Fact]
    public void RunNextDryRun_WithStoppedSession_DoesNotRewriteMirrorsOrClearStopState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, """
{
  "schema_version": 1,
  "session_id": "default",
  "attached_repo_root": "D:\\Projects\\CARVES.AI\\CARVES.Runtime",
  "status": "stopped",
  "loop_mode": "manual_tick",
  "dry_run": true,
  "active_worker_count": 0,
  "tick_count": 0,
  "active_task_ids": [],
  "review_pending_task_ids": [],
  "pending_permission_request_ids": [],
  "current_task_id": null,
  "last_task_id": null,
  "last_review_task_id": null,
  "last_reason": "integration stop",
  "loop_reason": "integration stop",
  "loop_actionability": "terminal",
  "waiting_reason": null,
  "waiting_actionability": "none",
  "stop_reason": "integration stop",
  "stop_actionability": "terminal",
  "planner_lifecycle_state": "idle",
  "planner_sleep_reason": "none",
  "planner_wake_reason": "none",
  "planner_escalation_reason": "none",
  "planner_lifecycle_reason": null,
  "pending_planner_wake_signals": [],
  "detected_opportunity_count": 0,
  "evaluated_opportunity_count": 0,
  "last_worker_failure_kind": "none",
  "last_recovery_action": "none",
  "started_at": "2026-03-31T00:00:00Z",
  "updated_at": "2026-03-31T00:00:00Z"
}
""");

        var currentTaskPath = Path.Combine(sandbox.RootPath, ".ai", "CURRENT_TASK.md");
        var statePath = Path.Combine(sandbox.RootPath, ".ai", "STATE.md");
        var taskQueuePath = Path.Combine(sandbox.RootPath, ".ai", "TASK_QUEUE.md");
        var beforeCurrentTask = File.ReadAllText(currentTaskPath);
        var beforeState = File.ReadAllText(statePath);
        var beforeTaskQueue = File.ReadAllText(taskQueuePath);
        var beforeSession = File.ReadAllText(sessionPath);

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(beforeCurrentTask, File.ReadAllText(currentTaskPath));
        Assert.Equal(beforeState, File.ReadAllText(statePath));
        Assert.Equal(beforeTaskQueue, File.ReadAllText(taskQueuePath));
        Assert.Equal(beforeSession, File.ReadAllText(sessionPath));
    }
}
