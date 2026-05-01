using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed class PlannerReentryIntegrationTests
{
    [Fact]
    public void SessionTick_ReentersPlanningWhenOpportunitiesCanBeMaterialized()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearPlannerGeneratedTasks();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var detect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "detect-refactors");
        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var tick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick", "--dry-run");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var graphJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "graph.json")))!.AsObject();
        var hasSuggestedPlanningTask = graphJson["tasks"]!.AsArray()
            .Select(task => task!.AsObject())
            .Any(task =>
                task["task_id"]!.GetValue<string>().StartsWith("T-PLAN-", StringComparison.Ordinal) &&
                string.Equals(task["status"]?.GetValue<string>(), "suggested", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, detect.ExitCode);
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, tick.ExitCode);
        Assert.Contains("Planner re-entry", tick.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("paused", sessionJson["status"]!.GetValue<string>());
        Assert.Equal("manual_tick", sessionJson["loop_mode"]!.GetValue<string>());
        Assert.True(hasSuggestedPlanningTask);
    }

    [Fact]
    public void SessionLoop_PausesAfterPlannerReentryAndPersistsWaitingReason()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearPlannerGeneratedTasks();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var detect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "detect-refactors");
        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var loop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "loop", "--dry-run", "--iterations", "3");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();

        Assert.Equal(0, detect.ExitCode);
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, loop.ExitCode);
        Assert.Contains("Continuous loop iterations:", loop.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner re-entry outcome", loop.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("paused", sessionJson["status"]!.GetValue<string>());
        Assert.Equal("continuous_loop", sessionJson["loop_mode"]!.GetValue<string>());
        Assert.Contains("Accepted", sessionJson["waiting_reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(1, sessionJson["planner_round"]!.GetValue<int>());
    }

    [Fact]
    public void SessionTick_ConsumesQueuedPlannerWakeSignalBeforeIdleFallback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearPlannerGeneratedTasks();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var detect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "detect-refactors");
        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var sessionRepository = new JsonRuntimeSessionRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        var session = sessionRepository.Load()!;
        session.EnqueuePlannerWake(
            PlannerWakeReason.WorkerResultReturned,
            PlannerWakeSourceKind.WorkerOutcome,
            "Worker returned a result for asynchronous planner follow-up.",
            "T-ASYNC-001: succeeded via codex_cli",
            taskId: "T-ASYNC-001",
            runId: "RUN-T-ASYNC-001");
        sessionRepository.Save(session);

        var tick = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "tick", "--dry-run");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();

        Assert.Equal(0, detect.ExitCode);
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, tick.ExitCode);
        Assert.Contains("Planner re-entry", tick.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("T-ASYNC-001: succeeded via codex_cli", sessionJson["last_consumed_planner_wake_summary"]!.GetValue<string>());
        Assert.Equal("worker_result_returned", sessionJson["planner_wake_reason"]!.GetValue<string>());
    }
}
