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
    public PlannerHostResult RunPlanner(bool dryRun, PlannerWakeReason wakeReason, string detail)
    {
        var session = GetSession() ?? StartSession(dryRun);
        var plannerSession = actorSessionService.Ensure(
            ActorSessionKind.Planner,
            "planner-host",
            ResolveRepoId(),
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "run");
        AcquirePlannerLease(session, PlannerLeaseMode.ManualRun, "planner-host", detail);
        session.BeginTick(dryRun, RuntimeLoopMode.ManualTick);
        try
        {
            var result = plannerHostService.RunOnce(session, wakeReason, detail);
            actorSessionService.MarkState(
                plannerSession.ActorSessionId,
                ResolvePlannerActorState(session.PlannerLifecycleState),
                session.PlannerLifecycleReason ?? detail,
                runtimeSessionId: session.SessionId,
                operationClass: "planner",
                operation: "run");
            PersistPlannerHostSession(session, result.Reentry);
            return result;
        }
        finally
        {
            ReleasePlannerLease(session, "planner-host run completed");
            sessionRepository.Save(session);
            markdownSyncService.Sync(taskGraphService.Load(), session: session);
        }
    }

    public PlannerHostLoopResult RunPlannerLoop(bool dryRun, int maxIterations, PlannerWakeReason wakeReason, string detail)
    {
        if (maxIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "Planner host iteration count must be positive.");
        }

        var session = GetSession() ?? StartSession(dryRun);
        var plannerSession = actorSessionService.Ensure(
            ActorSessionKind.Planner,
            "planner-host",
            ResolveRepoId(),
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "host");
        AcquirePlannerLease(session, PlannerLeaseMode.ContinuousLoop, "planner-host", detail);
        var iterations = new List<PlannerHostResult>();
        try
        {
            for (var index = 0; index < maxIterations; index++)
            {
                if (session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.ReviewWait or RuntimeSessionStatus.ApprovalWait or RuntimeSessionStatus.Failed or RuntimeSessionStatus.Stopped)
                {
                    break;
                }

                session.BeginTick(dryRun, RuntimeLoopMode.ContinuousLoop);
                var result = plannerHostService.RunOnce(session, index == 0 ? wakeReason : PlannerWakeReason.OpportunityDeltaDetected, index == 0 ? detail : "planner host loop continued");
                PersistPlannerHostSession(session, result.Reentry);
                actorSessionService.MarkState(
                    plannerSession.ActorSessionId,
                    ResolvePlannerActorState(session.PlannerLifecycleState),
                    session.PlannerLifecycleReason ?? detail,
                    runtimeSessionId: session.SessionId,
                    operationClass: "planner",
                    operation: "host");
                iterations.Add(result);

                if (session.PlannerLifecycleState is Carves.Runtime.Domain.Planning.PlannerLifecycleState.Sleeping or Carves.Runtime.Domain.Planning.PlannerLifecycleState.Waiting or Carves.Runtime.Domain.Planning.PlannerLifecycleState.Blocked or Carves.Runtime.Domain.Planning.PlannerLifecycleState.Escalated)
                {
                    break;
                }
            }
        }
        finally
        {
            ReleasePlannerLease(session, "planner-host loop completed");
            sessionRepository.Save(session);
            markdownSyncService.Sync(taskGraphService.Load(), session: session);
        }

        return new PlannerHostLoopResult
        {
            Iterations = iterations,
            Session = session,
            MaxIterations = maxIterations,
            Message = session.PlannerLifecycleReason ?? session.LastReason,
        };
    }

    public RuntimeSessionState WakePlanner(PlannerWakeReason wakeReason, string detail)
    {
        var session = RequireSession();
        var plannerSession = actorSessionService.Ensure(
            ActorSessionKind.Planner,
            "planner-host",
            ResolveRepoId(),
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "wake");
        session.WakePlanner(wakeReason, detail);
        actorSessionService.MarkState(
            plannerSession.ActorSessionId,
            ActorSessionState.Active,
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "wake");
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    public RuntimeSessionState SleepPlanner(Carves.Runtime.Domain.Planning.PlannerSleepReason sleepReason, string detail)
    {
        var session = RequireSession();
        var plannerSession = actorSessionService.Ensure(
            ActorSessionKind.Planner,
            "planner-host",
            ResolveRepoId(),
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "sleep");
        session.SleepPlanner(sleepReason, detail);
        actorSessionService.MarkState(
            plannerSession.ActorSessionId,
            ActorSessionState.Sleeping,
            detail,
            runtimeSessionId: session.SessionId,
            operationClass: "planner",
            operation: "sleep");
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    private PlannerReentryResult? TryPlannerReentry(RuntimeSessionState session, TaskScheduleDecision decision)
    {
        if (CanConsumeQueuedPlannerWake(session, decision)
            && plannerWakeBridgeService.TryConsume(session, out var signal))
        {
            return plannerHostService.RunOnce(session, signal!.WakeReason, signal.Detail).Reentry;
        }

        if (!decision.AllowsPlannerReentry)
        {
            return null;
        }

        return plannerHostService.RunOnce(
            session,
            PlannerWakeReason.ExecutionBacklogCleared,
            decision.Reason).Reentry;
    }

    private CycleResult PersistPlannerReentry(RuntimeSessionState session, TaskScheduleDecision decision, PlannerReentryResult reentry)
    {
        session.RecordPlannerReentry(
            reentry.Outcome.ToString(),
            reentry.ProposedTaskIds,
            reentry.Reason,
            reentry.RequiresOperatorPause ? RuntimeActionability.HumanActionable : RuntimeActionability.PlannerActionable,
            plannerRound: reentry.PlannerRound,
            detectedOpportunityCount: reentry.DetectedOpportunityCount,
            evaluatedOpportunityCount: reentry.EvaluatedOpportunityCount,
            opportunitySourceSummary: reentry.OpportunitySourceSummary,
            analysisReason: reentry.Reason);
        if (reentry.RequiresOperatorPause)
        {
            session.Pause(reentry.Reason, RuntimeActionability.HumanActionable);
        }
        else
        {
            session.MarkIdle(reentry.Reason, RuntimeActionability.PlannerActionable);
        }

        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session, schedulerDecision: decision);
        return new CycleResult
        {
            Session = session,
            ScheduleDecision = decision,
            PlannerReentry = reentry,
            Message = reentry.Message,
        };
    }

    private void PersistPlannerHostSession(RuntimeSessionState session, PlannerReentryResult reentry)
    {
        if (reentry.RequiresOperatorPause)
        {
            session.Pause(reentry.Reason, RuntimeActionability.HumanActionable);
        }
        else if (session.PlannerLifecycleState == Carves.Runtime.Domain.Planning.PlannerLifecycleState.Sleeping)
        {
            session.MarkIdle(reentry.Reason, RuntimeActionability.PlannerActionable);
        }
        else
        {
            session.MarkIdle(reentry.Reason, RuntimeActionability.PlannerActionable);
        }

        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
    }

    public RuntimeSessionState QueuePlannerWake(
        PlannerWakeReason wakeReason,
        PlannerWakeSourceKind sourceKind,
        string detail,
        string summary,
        string? taskId = null,
        string? runId = null)
    {
        var session = RequireSession();
        plannerWakeBridgeService.Queue(session, wakeReason, sourceKind, detail, summary, taskId, runId, persist: true);
        return session;
    }

    private void QueuePlannerWakeForCycle(RuntimeSessionState session, CycleResult cycle)
    {
        if (cycle.Task is null || cycle.Transition is null || cycle.Report is null)
        {
            return;
        }

        var workerResult = cycle.Report.WorkerExecution;
        var wakeReason = workerResult.Succeeded || workerResult.Status == WorkerExecutionStatus.ApprovalWait
            ? PlannerWakeReason.WorkerResultReturned
            : PlannerWakeReason.TaskFailed;
        var sourceKind = cycle.Transition.NextStatus == DomainTaskStatus.ApprovalWait
            ? PlannerWakeSourceKind.PermissionResolution
            : PlannerWakeSourceKind.WorkerOutcome;
        var summary = $"{cycle.Task.TaskId}: {workerResult.Status} via {workerResult.BackendId ?? "(unknown)"}";
        plannerWakeBridgeService.Queue(
            session,
            wakeReason,
            sourceKind,
            $"{summary}. {cycle.Transition.Reason}",
            summary,
            cycle.Task.TaskId,
            workerResult.RunId);

        foreach (var unlockedTaskId in ResolveUnlockedTaskIds(cycle.Task.TaskId))
        {
            plannerWakeBridgeService.Queue(
                session,
                PlannerWakeReason.DependencyUnlocked,
                PlannerWakeSourceKind.DependencyUnlock,
                $"Task {cycle.Task.TaskId} unlocked dependent task {unlockedTaskId}.",
                $"{unlockedTaskId} became ready after {cycle.Task.TaskId}.",
                unlockedTaskId,
                workerResult.RunId);
        }
    }

    private IReadOnlyList<string> ResolveUnlockedTaskIds(string completedTaskId)
    {
        var graph = taskGraphService.Load();
        var completed = graph.CompletedTaskIds();
        return graph.ListTasks()
            .Where(task => task.Status == DomainTaskStatus.Pending)
            .Where(task => task.Dependencies.Contains(completedTaskId, StringComparer.Ordinal))
            .Where(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completed))
            .Select(task => task.TaskId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanConsumeQueuedPlannerWake(RuntimeSessionState session, TaskScheduleDecision decision)
    {
        if (session.PendingPlannerWakeSignals.Count == 0)
        {
            return false;
        }

        if (session.Status is RuntimeSessionStatus.ReviewWait or RuntimeSessionStatus.ApprovalWait or RuntimeSessionStatus.Paused or RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Failed)
        {
            return false;
        }

        return decision.IdleReason is not (TaskScheduleIdleReason.ReviewBoundary or TaskScheduleIdleReason.SessionPaused or TaskScheduleIdleReason.SessionStopped);
    }

    private void AcquirePlannerLease(RuntimeSessionState session, PlannerLeaseMode leaseMode, string owner, string reason)
    {
        session.AcquirePlannerLease(leaseMode, owner, reason);
        plannerWakeBridgeService.RecordLeaseAcquired(session);
    }

    private void ReleasePlannerLease(RuntimeSessionState session, string reason)
    {
        session.ReleasePlannerLease(reason);
        plannerWakeBridgeService.RecordLeaseReleased(session);
    }

    private string ResolveRepoId()
    {
        return Path.GetFileName(attachedRepoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static ActorSessionState ResolvePlannerActorState(Carves.Runtime.Domain.Planning.PlannerLifecycleState state)
    {
        return state switch
        {
            Carves.Runtime.Domain.Planning.PlannerLifecycleState.Sleeping => ActorSessionState.Sleeping,
            Carves.Runtime.Domain.Planning.PlannerLifecycleState.Waiting => ActorSessionState.Waiting,
            Carves.Runtime.Domain.Planning.PlannerLifecycleState.Blocked or Carves.Runtime.Domain.Planning.PlannerLifecycleState.Escalated => ActorSessionState.Blocked,
            Carves.Runtime.Domain.Planning.PlannerLifecycleState.Idle => ActorSessionState.Idle,
            _ => ActorSessionState.Active,
        };
    }
}
