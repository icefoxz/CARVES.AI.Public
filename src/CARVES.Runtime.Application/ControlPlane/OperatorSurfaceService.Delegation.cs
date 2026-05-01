using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Orchestration;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public DelegatedExecutionResultEnvelope RunDelegatedTask(
        string taskId,
        bool dryRun,
        ActorSessionKind actorKind,
        string actorIdentity,
        bool manualFallback = false,
        WorkerSelectionOptions? selectionOptions = null)
    {
        if (actorKind == ActorSessionKind.Planner)
        {
            return DelegatedExecutionResultEnvelope.Rejected(
                taskId,
                actorKind,
                actorIdentity,
                outcome: "planner_execution_denied",
                summary: "PlannerSession is decision-only and cannot execute task runs.",
                nextAction: "Route execution through Host/Scheduler to an eligible WorkerSession.",
                guidance:
                [
                    "PlannerSession may decompose, sequence, and request re-entry, but it must not implement or run tasks.",
                    $"Use `task run {taskId}` from an operator-governed surface so Host can dispatch to a WorkerSession.",
                    "Worker result submission still requires Host ingestion, Review, and writeback gates."
                ]);
        }

        var roleExecutionGate = RuntimeRoleModeExecutionGate.EvaluateDelegatedExecution(
            runtimePolicyBundleService.LoadRoleGovernancePolicy());
        if (!roleExecutionGate.Allowed)
        {
            return DelegatedExecutionResultEnvelope.Rejected(
                taskId,
                actorKind,
                actorIdentity,
                outcome: roleExecutionGate.Outcome,
                summary: roleExecutionGate.Summary,
                nextAction: roleExecutionGate.NextAction,
                guidance: roleExecutionGate.Guidance);
        }

        var delegationPolicy = runtimePolicyBundleService.LoadDelegationPolicy();
        if (manualFallback && !delegationPolicy.AllowManualExecutionFallback)
        {
            return DelegatedExecutionResultEnvelope.Rejected(
                taskId,
                actorKind,
                actorIdentity,
                outcome: "manual_fallback_denied",
                summary: "Manual delegation fallback is disabled by externalized runtime policy.",
                nextAction: "Start or recover the resident host before retrying delegated execution.",
                guidance:
                [
                    "Use `host ensure --json` to check, start, or reconcile the resident host path.",
                    "Use `policy inspect` to review the current delegation rule."
                ]);
        }

        var reason = manualFallback
            ? $"Delegated execution fallback requested for {taskId}."
            : $"Delegated execution requested for {taskId}.";
        var actorSession = EnsureControlActorSession(
            actorKind,
            actorIdentity,
            reason,
            OwnershipScope.TaskMutation,
            taskId,
            operationClass: "delegation",
            operation: "run_task");
        var arbitration = concurrentActorArbitrationService.Resolve(actorSession, OwnershipScope.TaskMutation, taskId, reason);
        if (arbitration.Outcome != ActorArbitrationOutcome.Granted)
        {
            return DelegatedExecutionResultEnvelope.RejectedWithFallbackPacket(
                taskId,
                actorKind,
                actorIdentity,
                outcome: "rejected",
                summary: arbitration.Summary,
                nextAction: BuildOwnershipPollInstruction(OwnershipScope.TaskMutation, taskId),
                manualFallback: manualFallback,
                fallbackRunPacket: BuildFallbackRunPacket(taskId, actorSession, manualFallback, worker: null, runId: null),
                guidance:
                [
                    $"Use `actor ownership --scope TaskMutation --target-id {taskId}` to inspect the current owner.",
                    $"Poll `api actor-ownership --scope TaskMutation --target-id {taskId}` until the task is released, then retry delegated execution through the host."
                ]);
        }

        AppendDelegationEvent(
            manualFallback ? OperatorOsEventKind.DelegationFallbackUsed : OperatorOsEventKind.DelegationRequested,
            actorSession,
            taskId,
            null,
            manualFallback
                ? "manual_delegation_fallback"
                : "delegation_requested",
            manualFallback
                ? $"Manual delegation fallback was requested for {taskId}."
                : $"Delegated execution was requested for {taskId}.");
        ExecutionRun? executionRun = null;
        var executionRunSettled = false;
        try
        {
            var task = devLoopService.PreflightDelegatedTask(taskId, dryRun, reason);
            var existingRun = executionRunService.TryGetActiveRun(task);
            if (existingRun is not null && existingRun.Status == ExecutionRunStatus.Running)
            {
                var existingEnvelope = BuildExistingDelegatedExecutionResult(
                    task,
                    actorSession,
                    manualFallback,
                    devLoopService.GetSession(),
                    existingRun);
                actorSessionService.MarkState(
                    actorSession.ActorSessionId,
                    ActorSessionState.Waiting,
                    existingEnvelope.Summary,
                    runtimeSessionId: devLoopService.GetSession()?.SessionId,
                    taskId: taskId,
                    runId: existingRun.RunId,
                    operationClass: "delegation",
                    operation: "run_task");
                AppendDelegationEvent(
                    OperatorOsEventKind.DelegationCompleted,
                    actorSession,
                    taskId,
                    existingRun.RunId,
                    "delegation_existing_run",
                    existingEnvelope.Summary);
                return existingEnvelope;
            }

            executionRun = executionRunService.PrepareRunForDispatch(task);
            task = executionRunService.ApplyTaskMetadata(task, executionRun, executionRun.RunId);
            taskGraphService.ReplaceTask(task);
            markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
            new ExecutionEnvelopeService(paths).Generate(task, executionRun);
            var cycle = devLoopService.RunDelegatedTask(taskId, dryRun, reason, selectionOptions);
            var resultSubmission = SubmitDelegatedResultEnvelope(cycle.Report, executionRun, dryRun);
            var finalTask = resultSubmission.IngestionOutcome is null
                ? cycle.Task
                : taskGraphService.GetTask(taskId);
            if (finalTask is not null)
            {
                var finalRun = dryRun
                    ? executionRunService.AbandonRun(executionRun, "Dry-run execution does not mutate authoritative run truth.")
                    : resultSubmission.IngestionOutcome is not null
                        ? executionRunService.TryLoad(executionRun.RunId) ?? FinalizeExecutionRun(finalTask, executionRun, resultSubmission.ResultEnvelopePath)
                        : FinalizeExecutionRun(finalTask, executionRun, resultSubmission.ResultEnvelopePath);
                if (!dryRun
                    && resultSubmission.IngestionOutcome is null
                    && finalRun.Status is ExecutionRunStatus.Completed or ExecutionRunStatus.Failed or ExecutionRunStatus.Stopped)
                {
                    var runReportService = new ExecutionRunReportService(paths);
                    runReportService.Persist(finalRun, taskRunReport: cycle.Report);
                    finalTask = new ExecutionPatternGuardService().Apply(
                        finalTask,
                        new ExecutionPatternService().Analyze(finalTask.TaskId, runReportService.ListReports(finalTask.TaskId)));
                }

                finalTask = executionRunService.ApplyTaskMetadata(
                    finalTask,
                    finalRun,
                    finalRun.Status is ExecutionRunStatus.Planned or ExecutionRunStatus.Running ? finalRun.RunId : null);
                taskGraphService.ReplaceTask(finalTask);
                markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
                cycle = cycle with { Tasks = [finalTask] };
                executionRunSettled = true;
            }

            var envelope = BuildDelegatedExecutionResult(cycle, actorSession, manualFallback, executionRun, resultSubmission);
            actorSessionService.MarkState(
                actorSession.ActorSessionId,
                ResolveDelegationActorState(cycle),
                envelope.Summary,
                runtimeSessionId: cycle.Session?.SessionId,
                taskId: taskId,
                runId: cycle.Report?.WorkerExecution.RunId,
                operationClass: "delegation",
                operation: "run_task");
            AppendDelegationEvent(
                OperatorOsEventKind.DelegationCompleted,
                actorSession,
                taskId,
                cycle.Report?.WorkerExecution.RunId,
                envelope.Accepted ? "delegation_completed" : "delegation_rejected",
                envelope.Summary);
            return envelope;
        }
        catch (Exception exception)
        {
            if (executionRun is not null && !executionRunSettled)
            {
                var currentTask = taskGraphService.GetTask(taskId);
                var abandonedRun = executionRunService.AbandonRun(executionRun, exception.Message);
                currentTask = executionRunService.ApplyTaskMetadata(currentTask, abandonedRun, activeRunId: null);
                taskGraphService.ReplaceTask(currentTask);
                markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
            }

            actorSessionService.MarkState(
                actorSession.ActorSessionId,
                ActorSessionState.Blocked,
                exception.Message,
                runtimeSessionId: devLoopService.GetSession()?.SessionId,
                taskId: taskId,
                operationClass: "delegation",
                operation: "run_task");
            AppendDelegationEvent(
                OperatorOsEventKind.DelegationCompleted,
                actorSession,
                taskId,
                null,
                "delegation_failed",
                exception.Message);
            return DelegatedExecutionResultEnvelope.RejectedWithFallbackPacket(
                taskId,
                actorKind,
                actorIdentity,
                outcome: "failed",
                summary: exception.Message,
                nextAction: "Inspect the task and runtime failure before retrying delegated execution.",
                manualFallback: manualFallback,
                fallbackRunPacket: BuildFallbackRunPacket(taskId, actorSession, manualFallback, worker: null, runId: executionRun?.RunId),
                guidance:
                [
                    $"Use `task inspect {taskId}` to inspect the authoritative task state.",
                    "Review the runtime failure artifact before retrying."
                ]);
        }
        finally
        {
            sessionOwnershipService.Release(OwnershipScope.TaskMutation, taskId, $"Delegated execution ownership released for {taskId}.");
        }
    }

}
