using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Workers;

internal sealed class WorkerExecutionLifecycleRecorder
{
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly ActorSessionService actorSessionService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;
    private readonly IWorkerExecutionAuditReadModel? auditReadModel;

    public WorkerExecutionLifecycleRecorder(
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        IWorkerExecutionAuditReadModel? auditReadModel = null)
    {
        this.incidentTimelineService = incidentTimelineService;
        this.actorSessionService = actorSessionService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.auditReadModel = auditReadModel;
    }

    public WorkerExecutionLifecycle Start(WorkerRequest request)
    {
        var repoId = request.Selection?.RepoId ?? "local-repo";
        var workerSession = actorSessionService.Ensure(
            ActorSessionKind.Worker,
            request.Session.WorkerAdapterName,
            repoId,
            $"Worker session prepared for task '{request.Task.TaskId}'.",
            runtimeSessionId: request.Session.TaskId,
            operationClass: "worker",
            operation: "execute");
        incidentTimelineService.Append(new RuntimeIncidentRecord
        {
            IncidentType = RuntimeIncidentType.WorkerStarted,
            RepoId = repoId,
            TaskId = request.Task.TaskId,
            BackendId = request.Selection?.SelectedBackendId ?? request.ExecutionRequest?.BackendHint,
            ProviderId = request.Selection?.SelectedProviderId,
            ActorKind = RuntimeIncidentActorKind.Worker,
            ActorIdentity = request.Session.WorkerAdapterName,
            ReasonCode = "worker_started",
            Summary = $"Worker execution started for task '{request.Task.TaskId}'.",
            ConsequenceSummary = "Worker execution is in progress.",
        });
        actorSessionService.MarkState(
            workerSession.ActorSessionId,
            ActorSessionState.Active,
            $"Worker execution started for task '{request.Task.TaskId}'.",
            taskId: request.Task.TaskId,
            operationClass: "worker",
            operation: "execute");
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.TaskStarted,
            RepoId = repoId,
            ActorSessionId = workerSession.ActorSessionId,
            ActorKind = workerSession.Kind,
            ActorIdentity = workerSession.ActorIdentity,
            TaskId = request.Task.TaskId,
            ReferenceId = request.Task.TaskId,
            ReasonCode = "task_started",
            Summary = $"Worker execution started for task '{request.Task.TaskId}'.",
        });

        return new WorkerExecutionLifecycle(repoId, workerSession);
    }

    public void Complete(WorkerExecutionLifecycle lifecycle, WorkerRequest request, TaskRunReport finalReport)
    {
        var workerExecution = finalReport.WorkerExecution;
        incidentTimelineService.Append(new RuntimeIncidentRecord
        {
            IncidentType = workerExecution.Succeeded ? RuntimeIncidentType.WorkerCompleted : RuntimeIncidentType.WorkerFailed,
            RepoId = lifecycle.RepoId,
            TaskId = request.Task.TaskId,
            RunId = workerExecution.RunId,
            BackendId = workerExecution.BackendId,
            ProviderId = workerExecution.ProviderId,
            ProtocolFamily = workerExecution.ProtocolFamily,
            FailureKind = workerExecution.FailureKind,
            FailureLayer = workerExecution.FailureLayer,
            ActorKind = RuntimeIncidentActorKind.Worker,
            ActorIdentity = workerExecution.AdapterId,
            ReasonCode = workerExecution.Succeeded ? "worker_completed" : "worker_failed",
            Summary = workerExecution.Summary,
            ConsequenceSummary = workerExecution.Succeeded
                ? "Worker execution completed and validation/review will continue."
                : "Worker execution failed and will enter recovery handling.",
            ReferenceId = workerExecution.RunId,
        });
        actorSessionService.MarkState(
            lifecycle.WorkerSession.ActorSessionId,
            workerExecution.Succeeded ? ActorSessionState.Idle : ActorSessionState.Blocked,
            workerExecution.Summary,
            taskId: request.Task.TaskId,
            runId: workerExecution.RunId,
            operationClass: "worker",
            operation: workerExecution.Succeeded ? "completed" : "failed");
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.WorkerSpawned,
            RepoId = lifecycle.RepoId,
            ActorSessionId = lifecycle.WorkerSession.ActorSessionId,
            ActorKind = lifecycle.WorkerSession.Kind,
            ActorIdentity = lifecycle.WorkerSession.ActorIdentity,
            TaskId = request.Task.TaskId,
            RunId = workerExecution.RunId,
            BackendId = workerExecution.BackendId,
            ProviderId = workerExecution.ProviderId,
            ReferenceId = workerExecution.RunId,
            ReasonCode = "worker_spawned",
            Summary = $"Worker backend '{workerExecution.BackendId}' handled task '{request.Task.TaskId}'.",
        });
        if (!workerExecution.Succeeded)
        {
            operatorOsEventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.TaskFailed,
                RepoId = lifecycle.RepoId,
                ActorSessionId = lifecycle.WorkerSession.ActorSessionId,
                ActorKind = lifecycle.WorkerSession.Kind,
                ActorIdentity = lifecycle.WorkerSession.ActorIdentity,
                TaskId = request.Task.TaskId,
                RunId = workerExecution.RunId,
                BackendId = workerExecution.BackendId,
                ProviderId = workerExecution.ProviderId,
                ReferenceId = workerExecution.RunId,
                ReasonCode = "task_failed",
                Summary = workerExecution.Summary,
            });
        }

        TryAppendAuditEntry(lifecycle, finalReport);
    }

    private void TryAppendAuditEntry(WorkerExecutionLifecycle lifecycle, TaskRunReport finalReport)
    {
        if (auditReadModel is null)
        {
            return;
        }

        try
        {
            auditReadModel.AppendExecution(WorkerExecutionAuditEntry.From(finalReport));
        }
        catch (Exception exception)
        {
            // The SQLite read model is sidecar-only and must not affect canonical worker execution.
            TryRecordAuditAppendFailure(lifecycle, finalReport, exception);
        }
    }

    private void TryRecordAuditAppendFailure(WorkerExecutionLifecycle lifecycle, TaskRunReport finalReport, Exception exception)
    {
        try
        {
            var workerExecution = finalReport.WorkerExecution;
            incidentTimelineService.Append(new RuntimeIncidentRecord
            {
                IncidentType = RuntimeIncidentType.AuditSidecarFailed,
                RepoId = lifecycle.RepoId,
                TaskId = workerExecution.TaskId,
                RunId = workerExecution.RunId,
                BackendId = workerExecution.BackendId,
                ProviderId = workerExecution.ProviderId,
                ProtocolFamily = workerExecution.ProtocolFamily,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                ActorKind = RuntimeIncidentActorKind.System,
                ActorIdentity = "worker-execution-audit-read-model",
                ReasonCode = "worker_execution_audit_sidecar_append_failed",
                Summary = BuildAuditAppendFailureSummary(workerExecution, exception),
                ConsequenceSummary = "Canonical worker execution continues; the worker execution audit read model may be missing this run.",
                ReferenceId = workerExecution.RunId,
            });
        }
        catch
        {
            // Failure observability is best-effort and must not affect canonical worker execution.
        }
    }

    private string BuildAuditAppendFailureSummary(WorkerExecutionResult workerExecution, Exception exception)
    {
        var storagePath = TryResolveAuditStoragePath();
        return $"Worker execution audit sidecar append failed for task '{workerExecution.TaskId}' at '{storagePath}': {exception.GetType().Name}: {exception.Message}";
    }

    private string TryResolveAuditStoragePath()
    {
        try
        {
            return auditReadModel?.StoragePath ?? "(unknown)";
        }
        catch
        {
            return "(unavailable)";
        }
    }
}

internal sealed record WorkerExecutionLifecycle(string RepoId, ActorSessionRecord WorkerSession);
