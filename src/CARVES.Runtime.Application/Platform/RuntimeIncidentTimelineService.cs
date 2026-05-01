using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeIncidentTimelineService
{
    private readonly IRuntimeIncidentTimelineRepository repository;
    private readonly OperatorOsEventStreamService eventStreamService;

    public RuntimeIncidentTimelineService(IRuntimeIncidentTimelineRepository repository, OperatorOsEventStreamService eventStreamService)
    {
        this.repository = repository;
        this.eventStreamService = eventStreamService;
    }

    public IReadOnlyList<RuntimeIncidentRecord> Load(string? taskId = null, string? runId = null)
    {
        return repository.Load()
            .Where(record => string.IsNullOrWhiteSpace(taskId) || string.Equals(record.TaskId, taskId, StringComparison.Ordinal))
            .Where(record => string.IsNullOrWhiteSpace(runId) || string.Equals(record.RunId, runId, StringComparison.Ordinal))
            .OrderByDescending(record => record.OccurredAt)
            .ToArray();
    }

    public void Append(RuntimeIncidentRecord record)
    {
        var records = repository.Load().ToList();
        records.Add(record);
        repository.Save(records.OrderByDescending(item => item.OccurredAt).ToArray());
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = record.IncidentType switch
            {
                RuntimeIncidentType.RecoverySelected => OperatorOsEventKind.RecoverySelected,
                RuntimeIncidentType.ProviderHealthChanged => OperatorOsEventKind.ProviderHealthChanged,
                _ => OperatorOsEventKind.IncidentDetected,
            },
            RepoId = record.RepoId,
            ActorKind = record.ActorKind switch
            {
                RuntimeIncidentActorKind.Human => ActorSessionKind.Operator,
                RuntimeIncidentActorKind.Policy => ActorSessionKind.Operator,
                RuntimeIncidentActorKind.Worker => ActorSessionKind.Worker,
                _ => null,
            },
            ActorIdentity = record.ActorIdentity,
            TaskId = record.TaskId,
            RunId = record.RunId,
            BackendId = record.BackendId,
            ProviderId = record.ProviderId,
            PermissionRequestId = record.PermissionRequestId,
            IncidentId = record.IncidentId,
            ReferenceId = record.ReferenceId ?? record.IncidentId,
            ReasonCode = record.ReasonCode,
            Summary = record.Summary,
            OccurredAt = record.OccurredAt,
        });
    }

    public void AppendPermissionAudit(WorkerPermissionAuditRecord record)
    {
        Append(new RuntimeIncidentRecord
        {
            IncidentType = RuntimeIncidentType.PermissionEvent,
            RepoId = record.RepoId,
            TaskId = record.TaskId,
            RunId = record.RunId,
            BackendId = record.BackendId,
            ProviderId = record.ProviderId,
            PermissionRequestId = record.PermissionRequestId,
            FailureKind = WorkerFailureKind.None,
            RecoveryAction = WorkerRecoveryAction.None,
            ActorKind = record.ActorKind switch
            {
                WorkerPermissionDecisionActorKind.Policy => RuntimeIncidentActorKind.Policy,
                WorkerPermissionDecisionActorKind.Human => RuntimeIncidentActorKind.Human,
                WorkerPermissionDecisionActorKind.Provider => RuntimeIncidentActorKind.Provider,
                _ => RuntimeIncidentActorKind.System,
            },
            ActorIdentity = record.ActorIdentity,
            ReasonCode = record.ReasonCode,
            Summary = record.Reason,
            ConsequenceSummary = record.ConsequenceSummary,
            ReferenceId = record.AuditId,
            OccurredAt = record.OccurredAt,
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = record.Decision is null
                ? OperatorOsEventKind.PermissionRequested
                : OperatorOsEventKind.PermissionDecided,
            RepoId = record.RepoId,
            ActorKind = record.ActorKind switch
            {
                WorkerPermissionDecisionActorKind.Human => ActorSessionKind.Operator,
                WorkerPermissionDecisionActorKind.Provider => ActorSessionKind.Worker,
                _ => ActorSessionKind.Agent,
            },
            ActorIdentity = record.ActorIdentity,
            TaskId = record.TaskId,
            RunId = record.RunId,
            BackendId = record.BackendId,
            ProviderId = record.ProviderId,
            PermissionRequestId = record.PermissionRequestId,
            ReferenceId = record.AuditId,
            ReasonCode = record.ReasonCode,
            Summary = record.Reason,
            OccurredAt = record.OccurredAt,
        });
    }
}
