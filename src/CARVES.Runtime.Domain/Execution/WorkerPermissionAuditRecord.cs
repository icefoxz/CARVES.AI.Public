using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerPermissionAuditRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string AuditId { get; init; } = $"worker-permission-audit-{Guid.NewGuid():N}";

    public string RepoId { get; init; } = string.Empty;

    public string PermissionRequestId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public WorkerPermissionKind PermissionKind { get; init; } = WorkerPermissionKind.UnknownPermissionRequest;

    public WorkerPermissionRiskLevel RiskLevel { get; init; } = WorkerPermissionRiskLevel.High;

    public WorkerPermissionAuditEventKind EventKind { get; init; } = WorkerPermissionAuditEventKind.RequestObserved;

    public WorkerPermissionDecision? Decision { get; init; }

    public WorkerPermissionDecisionActorKind ActorKind { get; init; } = WorkerPermissionDecisionActorKind.System;

    public string ActorIdentity { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ConsequenceSummary { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
