namespace Carves.Runtime.Domain.Platform;

public enum RuntimeIncidentType
{
    WorkerStarted = 0,
    WorkerCompleted = 1,
    WorkerFailed = 2,
    RecoverySelected = 3,
    WorktreeQuarantined = 4,
    WorktreeRebuilt = 5,
    PermissionEvent = 6,
    ProviderHealthChanged = 7,
    OperatorIntervention = 8,
    AuditSidecarFailed = 9,
}
