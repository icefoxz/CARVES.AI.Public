using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class RuntimeIncidentRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string IncidentId { get; init; } = $"runtime-incident-{Guid.NewGuid():N}";

    public RuntimeIncidentType IncidentType { get; init; } = RuntimeIncidentType.WorkerFailed;

    public string RepoId { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? BackendId { get; init; }

    public string? ProviderId { get; init; }

    public string? ProtocolFamily { get; init; }

    public string? PermissionRequestId { get; init; }

    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.None;

    public WorkerFailureLayer FailureLayer { get; init; } = WorkerFailureLayer.None;

    public WorkerRecoveryAction RecoveryAction { get; init; } = WorkerRecoveryAction.None;

    public RuntimeIncidentActorKind ActorKind { get; init; } = RuntimeIncidentActorKind.System;

    public string ActorIdentity { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ConsequenceSummary { get; init; } = string.Empty;

    public string? ReferenceId { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
