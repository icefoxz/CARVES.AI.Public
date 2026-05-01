using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed partial class DevLoopService
{
    private static readonly TimeSpan ExecutionIsolationBudget = TimeSpan.FromMinutes(20);

    public TaskNode PreflightDelegatedTask(string taskId, bool dryRun, string reason)
    {
        EnsureSessionForDelegatedExecution(dryRun, reason, taskId);
        var graph = taskGraphService.Load();
        if (!graph.Tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        if (!task.CanExecuteInWorker)
        {
            throw new InvalidOperationException($"Task '{taskId}' cannot execute in the worker runtime.");
        }

        if (task.Status != DomainTaskStatus.Pending)
        {
            throw new InvalidOperationException($"Task '{taskId}' is not pending and cannot be delegated.");
        }

        var completed = graph.CompletedTaskIds();
        if (!task.IsReady(completed))
        {
            var unresolved = task.Dependencies.Where(dependency => !completed.Contains(dependency)).ToArray();
            var reasonText = unresolved.Length == 0
                ? $"Task '{taskId}' is not ready for delegated execution."
                : $"Task '{taskId}' is waiting on dependencies: {string.Join(", ", unresolved)}.";
            throw new InvalidOperationException(reasonText);
        }

        new ModeExecutionEntryGateService(formalPlanningExecutionGateService).EnsureReadyForExecution(task);
        return task;
    }

    public CycleResult RunDelegatedTask(string taskId, bool dryRun, string reason, WorkerSelectionOptions? selectionOptions = null)
    {
        var task = PreflightDelegatedTask(taskId, dryRun, reason);
        var session = RequireSession();

        session.BeginTick(dryRun, RuntimeLoopMode.ManualTick);
        var lease = workerBroker.Acquire(session, task.TaskId);
        if (!lease.Acquired)
        {
            throw new InvalidOperationException($"No worker lease could be acquired for '{task.TaskId}'.");
        }

        session.MarkExecuting([task.TaskId], reason);
        sessionRepository.Save(session);

        CycleResult? cycle = null;
        var preserveLeaseForIsolationTimeout = false;
        var failureAlreadyRecorded = false;
        try
        {
            var outcome = ExecuteTaskWithIsolation(task, dryRun, selectionOptions);
            if (outcome.Exception is not null)
            {
                preserveLeaseForIsolationTimeout = outcome.Exception is TaskExecutionIsolationTimeoutException;
                HandleFailure(session, task.TaskId, outcome.Exception);
                failureAlreadyRecorded = true;
                throw outcome.Exception;
            }

            cycle = outcome.Cycle ?? throw new InvalidOperationException($"Delegated execution for '{task.TaskId}' produced no cycle result.");
            UpdateSessionAfterCycles(session, [cycle]);
            if (cycle.Task is not null
                && cycle.Report is not null
                && !cycle.Report.WorkerExecution.Succeeded
                && cycle.Report.WorkerExecution.Status is not (WorkerExecutionStatus.Skipped or WorkerExecutionStatus.ApprovalWait))
            {
                HandleWorkerFailure(session, cycle.Request, cycle.Task, cycle.Report.WorkerExecution);
            }

            return cycle with
            {
                Session = session,
                Message = $"Delegated execution processed {task.TaskId}.",
            };
        }
        catch (Exception exception)
        {
            if (!failureAlreadyRecorded)
            {
                HandleFailure(session, task.TaskId, exception);
            }

            throw;
        }
        finally
        {
            if (!preserveLeaseForIsolationTimeout)
            {
                workerBroker.Release(session, lease);
            }

            sessionRepository.Save(session);
            markdownSyncService.Sync(
                taskGraphService.Load(),
                cycle?.Task,
                cycle?.Report,
                cycle?.Review,
                session,
                schedulerDecision: null);
        }
    }

    private CycleResult Tick(bool dryRun, RuntimeLoopMode loopMode)
    {
        var existingSession = GetSession();
        var createdEphemeralSession = existingSession is null;
        var session = existingSession ?? RuntimeSessionState.Start(attachedRepoRoot, dryRun);
        IReadOnlyList<CycleResult> completedCycles = Array.Empty<CycleResult>();
        IReadOnlySet<string> timedOutTaskIds = new HashSet<string>(StringComparer.Ordinal);

        if (dryRun && session.Status == RuntimeSessionStatus.Stopped)
        {
            var stoppedDecision = taskGraphService.DecideNext(session, workerBroker.Snapshot(session));
            return new CycleResult
            {
                Session = session,
                ScheduleDecision = stoppedDecision,
                Message = stoppedDecision.Reason,
            };
        }

        session.BeginTick(dryRun, loopMode);

        var snapshot = workerBroker.Snapshot(session);
        var decision = taskGraphService.DecideNext(session, snapshot);
        if (!decision.ShouldDispatch)
        {
            if (dryRun && createdEphemeralSession)
            {
                session.MarkIdle(decision.Reason, ResolveIdleActionability(decision));
                return new CycleResult
                {
                    Session = session,
                    ScheduleDecision = decision,
                    Message = decision.Reason,
                };
            }

            var reentry = TryPlannerReentry(session, decision);
            if (reentry is not null)
            {
                return PersistPlannerReentry(session, decision, reentry);
            }

            PersistIdleSession(session, decision, decision.Reason);
            return new CycleResult
            {
                Session = session,
                ScheduleDecision = decision,
                Message = decision.Reason,
            };
        }

        session.MarkScheduling(decision.Reason);
        var leases = AcquireLeases(session, decision.Tasks);
        if (leases.Count == 0)
        {
            var leaseDecision = TaskScheduleDecision.Idle("No worker lease could be acquired.", session.ActiveWorkerCount, snapshot.MaxWorkers);
            PersistIdleSession(session, leaseDecision, leaseDecision.Reason);
            return new CycleResult
            {
                Session = session,
                ScheduleDecision = leaseDecision,
                Message = leaseDecision.Reason,
            };
        }

        session.MarkExecuting(leases.Select(lease => lease.TaskId!).ToArray(), decision.Reason);
        sessionRepository.Save(session);

        try
        {
            var outcomes = ExecuteSelectedTasks(decision.Tasks, dryRun);
            timedOutTaskIds = outcomes
                .Where(outcome => outcome.Exception is TaskExecutionIsolationTimeoutException)
                .Select(outcome => outcome.Task.TaskId)
                .ToHashSet(StringComparer.Ordinal);
            completedCycles = outcomes.Where(outcome => outcome.Cycle is not null).Select(outcome => outcome.Cycle!).ToArray();
            UpdateSessionAfterCycles(session, completedCycles);
            foreach (var cycle in completedCycles.Where(cycle =>
                         cycle.Task is not null
                         && cycle.Report is not null
                         && !cycle.Report.WorkerExecution.Succeeded
                         && cycle.Report.WorkerExecution.Status is not (WorkerExecutionStatus.Skipped or WorkerExecutionStatus.ApprovalWait)))
            {
                HandleWorkerFailure(session, cycle.Request, cycle.Task!, cycle.Report!.WorkerExecution);
            }

            foreach (var outcome in outcomes.Where(outcome => outcome.Exception is not null))
            {
                HandleFailure(session, outcome.Task.TaskId, outcome.Exception!);
            }

            if (outcomes.Any(outcome => outcome.Exception is not null))
            {
                throw new AggregateException(outcomes.Where(outcome => outcome.Exception is not null).Select(outcome => outcome.Exception!));
            }

            return CombineCycles(completedCycles, session, decision);
        }
        catch (AggregateException)
        {
            throw;
        }
        catch (Exception exception)
        {
            HandleFailure(session, decision.Task?.TaskId ?? session.CurrentTaskId, exception);
            throw;
        }
        finally
        {
            foreach (var lease in leases)
            {
                if (lease.TaskId is not null && timedOutTaskIds.Contains(lease.TaskId))
                {
                    continue;
                }

                workerBroker.Release(session, lease);
            }

            sessionRepository.Save(session);
            markdownSyncService.Sync(
                taskGraphService.Load(),
                completedCycles.FirstOrDefault()?.Task,
                completedCycles.FirstOrDefault()?.Report,
                completedCycles.FirstOrDefault()?.Review,
                session,
                decision);
        }
    }

    private void PersistIdleSession(RuntimeSessionState session, TaskScheduleDecision decision, string reason)
    {
        if (session.Status != RuntimeSessionStatus.Paused && session.Status != RuntimeSessionStatus.ApprovalWait && session.Status != RuntimeSessionStatus.Stopped)
        {
            session.MarkIdle(reason, ResolveIdleActionability(decision));
        }

        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session, schedulerDecision: decision);
    }

    private void UpdateSessionAfterCycles(RuntimeSessionState session, IReadOnlyList<CycleResult> cycles)
    {
        if (cycles.Count == 0)
        {
            session.MarkIdle("No task completed during this tick.", RuntimeActionability.WorkerActionable);
            return;
        }

        foreach (var cycle in cycles)
        {
            if (cycle.Task is null || cycle.Transition is null || cycle.Report is null)
            {
                continue;
            }

            session.RecordWorkerOutcome(cycle.Report.WorkerExecution, cycle.Transition.Reason, ResolveWorkerActionability(cycle));
            QueuePlannerWakeForCycle(session, cycle);

            if (cycle.Transition.NextStatus == DomainTaskStatus.Review)
            {
                session.MarkReviewWait(cycle.Task.TaskId, cycle.Transition.Reason);
                continue;
            }

            if (cycle.Transition.NextStatus == DomainTaskStatus.ApprovalWait)
            {
                session.MarkApprovalWait(
                    cycle.Task.TaskId,
                    cycle.Report.WorkerExecution.PermissionRequests
                        .Where(item => item.State == Carves.Runtime.Domain.Execution.WorkerPermissionState.Pending)
                        .Select(item => item.PermissionRequestId)
                        .ToArray(),
                    cycle.Transition.Reason);
                continue;
            }

            session.MarkIdle(cycle.Transition.Reason, RuntimeActionability.WorkerActionable);
        }
    }

    private static RuntimeActionability ResolveWorkerActionability(CycleResult cycle)
    {
        if (cycle.Transition is null || cycle.Report is null)
        {
            return RuntimeActionability.WorkerActionable;
        }

        if (cycle.Transition.NextStatus == DomainTaskStatus.Review)
        {
            return RuntimeActionability.HumanActionable;
        }

        if (cycle.Transition.NextStatus == DomainTaskStatus.ApprovalWait)
        {
            return RuntimeActionability.HumanActionable;
        }

        if (!cycle.Report.WorkerExecution.Succeeded && !cycle.Report.WorkerExecution.Retryable)
        {
            return RuntimeActionability.HumanActionable;
        }

        return RuntimeActionability.WorkerActionable;
    }

    private static RuntimeActionability ResolveIdleActionability(TaskScheduleDecision decision)
    {
        return decision.IdleReason switch
        {
            TaskScheduleIdleReason.ReviewBoundary => RuntimeActionability.HumanActionable,
            TaskScheduleIdleReason.NoReadyExecutionTask => RuntimeActionability.PlannerActionable,
            TaskScheduleIdleReason.AllCandidatesBlocked => RuntimeActionability.HumanActionable,
            TaskScheduleIdleReason.SessionStopped => RuntimeActionability.Terminal,
            _ => RuntimeActionability.WorkerActionable,
        };
    }

    private RuntimeSessionState RequireSession()
    {
        return sessionRepository.Load() ?? throw new InvalidOperationException("No runtime session is attached.");
    }

    private RuntimeSessionState EnsureSessionForDelegatedExecution(bool dryRun, string reason, string? taskId = null)
    {
        var session = sessionRepository.Load();
        if (session is null || session.Status is RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Failed)
        {
            return StartSession(dryRun);
        }

        if (session.Status == RuntimeSessionStatus.Paused)
        {
            return ResumeSession(reason);
        }

        if (session.Status == RuntimeSessionStatus.ReviewWait)
        {
            session = ReconcileReviewBoundary() ?? session;
        }

        if (session.Status == RuntimeSessionStatus.ApprovalWait && session.PendingPermissionRequestIds.Count == 0)
        {
            session.MarkIdle("Reconciled stale approval boundary before delegated execution.", RuntimeActionability.WorkerActionable);
            sessionRepository.Save(session);
        }

        if (session.Status is RuntimeSessionStatus.ReviewWait or RuntimeSessionStatus.ApprovalWait)
        {
            throw BuildGovernanceBoundaryException(session, taskId);
        }

        return session;
    }

    private static InvalidOperationException BuildGovernanceBoundaryException(RuntimeSessionState session, string? taskId)
    {
        var reviewPendingTaskIds = session.ReviewPendingTaskIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (reviewPendingTaskIds.Length > 0)
        {
            var externalTaskIds = string.IsNullOrWhiteSpace(taskId)
                ? reviewPendingTaskIds
                : reviewPendingTaskIds.Where(item => !string.Equals(item, taskId, StringComparison.Ordinal)).ToArray();
            if (externalTaskIds.Length > 0)
            {
                return new InvalidOperationException(
                    $"Runtime session is waiting at the governance boundary for other review task(s) [{string.Join(", ", externalTaskIds)}]: {session.LastReason}");
            }

            return new InvalidOperationException(
                $"Runtime session is waiting at the governance boundary for {string.Join(", ", reviewPendingTaskIds)}: {session.LastReason}");
        }

        if (session.PendingPermissionRequestIds.Count > 0)
        {
            return new InvalidOperationException(
                $"Runtime session is waiting at the approval boundary for permission request(s) [{string.Join(", ", session.PendingPermissionRequestIds)}]: {session.LastReason}");
        }

        return new InvalidOperationException($"Runtime session is waiting at the governance boundary: {session.LastReason}");
    }

    private List<WorkerLease> AcquireLeases(RuntimeSessionState session, IReadOnlyList<TaskNode> tasks)
    {
        var leases = new List<WorkerLease>();
        foreach (var task in tasks)
        {
            var lease = workerBroker.Acquire(session, task.TaskId);
            if (!lease.Acquired)
            {
                break;
            }

            leases.Add(lease);
        }

        return leases;
    }

    private IReadOnlyList<DispatchOutcome> ExecuteSelectedTasks(IReadOnlyList<TaskNode> tasks, bool dryRun)
    {
        var outcomes = new List<DispatchOutcome>(tasks.Count);
        var pending = new Dictionary<string, ScheduledDispatch>(StringComparer.Ordinal);
        var sync = new object();
        var completedQueue = new Queue<ScheduledDispatch>();

        foreach (var task in tasks)
        {
            try
            {
                var prepared = plannerWorkerCycle.Prepare(task, dryRun);
                pending[task.TaskId] = ScheduledDispatch.Start(
                    prepared,
                    () => ExecutePreparedTask(prepared),
                    completed =>
                    {
                        lock (sync)
                        {
                            completedQueue.Enqueue(completed);
                            Monitor.PulseAll(sync);
                        }
                    });
            }
            catch (Exception exception)
            {
                outcomes.Add(new DispatchOutcome(task, null, exception));
            }
        }

        var deadline = DateTimeOffset.UtcNow.Add(ExecutionIsolationBudget);

        while (pending.Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            while (TryDequeueCompletedDispatch(sync, completedQueue, out var completed))
            {
                pending.Remove(completed.Prepared.Task.TaskId);
                outcomes.Add(MaterializeDispatchOutcome(completed.Execution));
            }

            if (pending.Count == 0)
            {
                break;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || !WaitForCompletedDispatch(sync, completedQueue, remaining))
            {
                break;
            }
        }

        while (TryDequeueCompletedDispatch(sync, completedQueue, out var completed))
        {
            pending.Remove(completed.Prepared.Task.TaskId);
            outcomes.Add(MaterializeDispatchOutcome(completed.Execution));
        }

        foreach (var execution in pending.Values)
        {
            outcomes.Add(new DispatchOutcome(
                execution.Prepared.Task,
                null,
                new TaskExecutionIsolationTimeoutException(execution.Prepared.Task.TaskId, ExecutionIsolationBudget)));
        }

        return outcomes;
    }

    private ExecutedDispatch ExecutePreparedTask(PreparedWorkerCycle prepared)
    {
        try
        {
            return new ExecutedDispatch(prepared, plannerWorkerCycle.Execute(prepared), null);
        }
        catch (Exception exception)
        {
            return new ExecutedDispatch(prepared, null, exception);
        }
    }

    private DispatchOutcome MaterializeDispatchOutcome(ExecutedDispatch executed)
    {
        if (executed.Exception is not null)
        {
            return new DispatchOutcome(executed.Prepared.Task, null, executed.Exception);
        }

        try
        {
            var cycle = plannerWorkerCycle.Complete(
                executed.Prepared,
                executed.Report ?? throw new InvalidOperationException($"Prepared execution for '{executed.Prepared.Task.TaskId}' completed without a report."));
            return new DispatchOutcome(executed.Prepared.Task, cycle, null);
        }
        catch (Exception exception)
        {
            return new DispatchOutcome(executed.Prepared.Task, null, exception);
        }
    }

    private DispatchOutcome ExecuteTaskWithIsolation(TaskNode task, bool dryRun, WorkerSelectionOptions? selectionOptions)
    {
        var sync = new object();
        DispatchOutcome? outcome = null;
        var completed = false;

        StartBackgroundThread(
            () =>
            {
                var execution = ExecuteTask(task, dryRun, selectionOptions);
                lock (sync)
                {
                    outcome = execution;
                    completed = true;
                    Monitor.PulseAll(sync);
                }
            },
            $"CARVES-Isolation-{task.TaskId}");

        lock (sync)
        {
            while (!completed)
            {
                if (!Monitor.Wait(sync, ExecutionIsolationBudget) && !completed)
                {
                    return new DispatchOutcome(task, null, new TaskExecutionIsolationTimeoutException(task.TaskId, ExecutionIsolationBudget));
                }
            }
        }

        return outcome ?? new DispatchOutcome(task, null, new TaskExecutionIsolationTimeoutException(task.TaskId, ExecutionIsolationBudget));
    }

    private DispatchOutcome ExecuteTask(TaskNode task, bool dryRun, WorkerSelectionOptions? selectionOptions = null)
    {
        try
        {
            return new DispatchOutcome(task, plannerWorkerCycle.Run(task, dryRun, selectionOptions), null);
        }
        catch (Exception exception)
        {
            return new DispatchOutcome(task, null, exception);
        }
    }

    private static CycleResult CombineCycles(IReadOnlyList<CycleResult> cycles, RuntimeSessionState session, TaskScheduleDecision decision)
    {
        if (cycles.Count == 1)
        {
            return cycles[0] with { Session = session, ScheduleDecision = decision };
        }

        return new CycleResult
        {
            Tasks = cycles.SelectMany(cycle => cycle.Tasks).ToArray(),
            Requests = cycles.SelectMany(cycle => cycle.Requests).ToArray(),
            Reports = cycles.SelectMany(cycle => cycle.Reports).ToArray(),
            Reviews = cycles.SelectMany(cycle => cycle.Reviews).ToArray(),
            Transitions = cycles.SelectMany(cycle => cycle.Transitions).ToArray(),
            Session = session,
            ScheduleDecision = decision,
            Message = $"Processed {cycles.Count} tasks: {string.Join(", ", cycles.SelectMany(cycle => cycle.Tasks).Select(task => task.TaskId))}.",
        };
    }

    private static bool WaitForCompletedDispatch(object sync, Queue<ScheduledDispatch> completedQueue, TimeSpan timeout)
    {
        lock (sync)
        {
            if (completedQueue.Count > 0)
            {
                return true;
            }

            return Monitor.Wait(sync, timeout);
        }
    }

    private static bool TryDequeueCompletedDispatch(object sync, Queue<ScheduledDispatch> completedQueue, out ScheduledDispatch completed)
    {
        lock (sync)
        {
            if (completedQueue.Count > 0)
            {
                completed = completedQueue.Dequeue();
                return true;
            }
        }

        completed = null!;
        return false;
    }

    private static void StartBackgroundThread(Action work, string name)
    {
        var thread = new Thread(() => work())
        {
            IsBackground = true,
            Name = name,
        };
        thread.Start();
    }

    private sealed record DispatchOutcome(TaskNode Task, CycleResult? Cycle, Exception? Exception);

    private sealed record ExecutedDispatch(PreparedWorkerCycle Prepared, TaskRunReport? Report, Exception? Exception);

    private sealed class ScheduledDispatch
    {
        private ScheduledDispatch(PreparedWorkerCycle prepared)
        {
            Prepared = prepared;
        }

        public PreparedWorkerCycle Prepared { get; }

        public ExecutedDispatch Execution { get; private set; } = null!;

        public static ScheduledDispatch Start(
            PreparedWorkerCycle prepared,
            Func<ExecutedDispatch> execute,
            Action<ScheduledDispatch> onCompleted)
        {
            var scheduled = new ScheduledDispatch(prepared);
            StartBackgroundThread(
                () =>
                {
                    try
                    {
                        scheduled.Execution = execute();
                    }
                    catch (Exception exception)
                    {
                        scheduled.Execution = new ExecutedDispatch(prepared, null, exception);
                    }

                    onCompleted(scheduled);
                },
                $"CARVES-Dispatch-{prepared.Task.TaskId}");
            return scheduled;
        }
    }
}

public sealed class TaskExecutionIsolationTimeoutException : TimeoutException
{
    public TaskExecutionIsolationTimeoutException(string taskId, TimeSpan timeout)
        : base($"Delegated execution for '{taskId}' exceeded the resident isolation budget of {timeout.TotalMinutes:0} minute(s); the runtime session was isolated and paused for operator recovery.")
    {
        TaskId = taskId;
        IsolationTimeout = timeout;
    }

    public string TaskId { get; }

    public TimeSpan IsolationTimeout { get; }
}
