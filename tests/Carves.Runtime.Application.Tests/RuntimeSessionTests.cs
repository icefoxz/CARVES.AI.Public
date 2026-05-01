using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using AppTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionTests
{
    [Fact]
    public void RuntimeSessionRepository_RoundTripsVersionedState()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);
        session.BeginTick(dryRun: true);
        session.MarkIdle("No ready task.");

        repository.Save(session);
        var loaded = repository.Load();

        Assert.NotNull(loaded);
        Assert.Equal(RuntimeSessionStatus.Idle, loaded!.Status);
        Assert.Equal(workspace.RootPath, loaded.AttachedRepoRoot);
        Assert.True(loaded.DryRun);
        Assert.Equal(RuntimeLoopMode.ManualTick, loaded.LoopMode);
        Assert.Equal("No ready task.", loaded.LoopReason);
        Assert.Equal("No ready task.", loaded.LastReason);
        Assert.Equal(RuntimeActionability.WorkerActionable, loaded.LoopActionability);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(workspace.Paths.RuntimeSessionFile), StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSessionRepository_RoundTripsActionabilityFields()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);
        session.Pause("Planner re-entry awaits operator approval.", RuntimeActionability.HumanActionable);

        repository.Save(session);
        var paused = repository.Load();

        Assert.NotNull(paused);
        Assert.Equal(RuntimeActionability.HumanActionable, paused!.WaitingActionability);
        Assert.Equal(RuntimeActionability.HumanActionable, paused.CurrentActionability);

        session.Stop("Session closed after operator decision.", RuntimeActionability.Terminal);
        repository.Save(session);
        var stopped = repository.Load();

        Assert.NotNull(stopped);
        Assert.Equal(RuntimeActionability.Terminal, stopped!.StopActionability);
        Assert.Equal(RuntimeActionability.None, stopped.WaitingActionability);
        Assert.Equal(RuntimeActionability.Terminal, stopped.CurrentActionability);
    }

    [Fact]
    public void RuntimeSessionRepository_RoundTripsPlannerRoundAndOpportunityState()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);
        session.RecordPlannerReentry(
            "SuggestedPlanningWork",
            ["T-PLAN-001"],
            "Planner materialized opportunity work.",
            plannerRound: 2,
            detectedOpportunityCount: 4,
            evaluatedOpportunityCount: 2,
            opportunitySourceSummary: "TestCoverage, MemoryDrift",
            analysisReason: "opportunity evaluation completed");

        repository.Save(session);
        var loaded = repository.Load();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.PlannerRound);
        Assert.Equal(4, loaded.DetectedOpportunityCount);
        Assert.Equal(2, loaded.EvaluatedOpportunityCount);
        Assert.Equal("TestCoverage, MemoryDrift", loaded.LastOpportunitySource);
        Assert.Equal("opportunity evaluation completed", loaded.AnalysisReason);
    }

    [Fact]
    public void RuntimeSessionRepository_RoundTripsPlannerWakeQueueAndLeaseState()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);
        session.AcquirePlannerLease(Carves.Runtime.Domain.Planning.PlannerLeaseMode.AsyncReentry, "planner-host", "queued worker result");
        session.EnqueuePlannerWake(
            Carves.Runtime.Domain.Planning.PlannerWakeReason.WorkerResultReturned,
            Carves.Runtime.Domain.Planning.PlannerWakeSourceKind.WorkerOutcome,
            "Worker returned a result for T-ASYNC-001.",
            "T-ASYNC-001: succeeded via codex_cli",
            taskId: "T-ASYNC-001",
            runId: "RUN-ASYNC-001");
        Assert.True(session.TryConsumePlannerWake(out var consumed));
        session.ReleasePlannerLease("planner wake consumed");

        repository.Save(session);
        var loaded = repository.Load();

        Assert.NotNull(loaded);
        Assert.False(loaded!.PlannerLeaseActive);
        Assert.Equal(Carves.Runtime.Domain.Planning.PlannerLeaseMode.AsyncReentry, loaded.PlannerLeaseMode);
        Assert.Equal("planner-host", loaded.PlannerLeaseOwner);
        Assert.NotNull(loaded.PlannerLeaseId);
        Assert.Empty(loaded.PendingPlannerWakeSignals);
        Assert.Equal(consumed!.SignalId, loaded.LastConsumedPlannerWakeSignalId);
        Assert.Equal(consumed.Summary, loaded.LastConsumedPlannerWakeSummary);
    }

    [Fact]
    public void RuntimeSessionRepository_ReadsLegacySessionPathAndWritesToLiveStateRoot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var legacyPath = Path.Combine(workspace.Paths.RuntimeRoot, "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, $$"""
        {
          "schema_version": 1,
          "session_id": "default",
          "attached_repo_root": "{{workspace.RootPath.Replace("\\", "\\\\", StringComparison.Ordinal)}}",
          "status": "idle",
          "loop_mode": "manual_tick",
          "dry_run": true
        }
        """);

        var loaded = repository.Load();

        Assert.NotNull(loaded);
        Assert.Equal(RuntimeSessionStatus.Idle, loaded!.Status);

        repository.Save(loaded);

        Assert.True(File.Exists(workspace.Paths.RuntimeSessionFile));
    }

    [Fact]
    public void WorktreeRuntimeRepository_ReadsLegacyPathAndPersistsToLiveStateRoot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonWorktreeRuntimeRepository(workspace.Paths);
        var legacyPath = Path.Combine(workspace.Paths.RuntimeRoot, "worktrees.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """
        {
          "schema_version": 1,
          "records": [
            {
              "schema_version": 1,
              "record_id": "record-T-LEGACY",
              "task_id": "T-LEGACY",
              "worktree_path": "D:/temp/worktree",
              "repo_root": "D:/temp/repo",
              "base_commit": "abc123",
              "state": "active",
              "worker_run_id": "run-legacy",
              "created_at": "2026-03-31T00:00:00+00:00",
              "updated_at": "2026-03-31T00:00:00+00:00"
            }
          ],
          "pending_rebuilds": []
        }
        """);

        var loaded = repository.Load();

        Assert.Single(loaded.Records);

        repository.Save(loaded);

        Assert.True(File.Exists(workspace.Paths.RuntimeWorktreeStateFile));
        Assert.Equal("T-LEGACY", loaded.Records[0].TaskId);
    }

    [Fact]
    public void TaskScheduler_ProvidesDeterministicDispatchAndIdleReasons()
    {
        var scheduler = new AppTaskScheduler();
        var task = new TaskNode
        {
            TaskId = "T-SCHEDULER",
            Title = "Scheduler fixture",
            Status = DomainTaskStatus.Pending,
            Priority = "P1",
            AcceptanceContract = CreateAcceptanceContract("T-SCHEDULER"),
        };
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph([task], ["CARD-SCHEDULER"]);
        var session = RuntimeSessionState.Start("C:\\repo", dryRun: false);

        var dispatch = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 1, Array.Empty<string>()));
        Assert.True(dispatch.ShouldDispatch);
        Assert.Equal("T-SCHEDULER", dispatch.Task?.TaskId);
        Assert.Equal(TaskScheduleIdleReason.None, dispatch.IdleReason);

        session.Pause("Human requested pause.");
        var paused = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 1, Array.Empty<string>()));

        Assert.False(paused.ShouldDispatch);
        Assert.Equal(TaskScheduleIdleReason.SessionPaused, paused.IdleReason);
        Assert.Contains("paused", paused.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static AcceptanceContract CreateAcceptanceContract(string taskId)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Acceptance for {taskId}",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            Intent = new AcceptanceContractIntent
            {
                Goal = "Allow the scheduler fixture to represent a governed executable task.",
                BusinessValue = "Keep deterministic dispatch tests aligned with acceptance-contract execution gating.",
            },
            Traceability = new AcceptanceContractTraceability
            {
                SourceTaskId = taskId,
            },
        };
    }
}
