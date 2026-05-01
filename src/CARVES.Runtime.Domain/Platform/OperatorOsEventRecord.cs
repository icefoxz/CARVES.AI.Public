using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class OperatorOsEventRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string EventId { get; init; } = $"operator-os-event-{Guid.NewGuid():N}";

    public OperatorOsEventKind EventKind { get; init; } = OperatorOsEventKind.IncidentDetected;

    public string RepoId { get; init; } = string.Empty;

    public string? ActorSessionId { get; init; }

    public ActorSessionKind? ActorKind { get; init; }

    public string? ActorIdentity { get; init; }

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? BackendId { get; init; }

    public string? ProviderId { get; init; }

    public string? PermissionRequestId { get; init; }

    public OwnershipScope? OwnershipScope { get; init; }

    public string? OwnershipTargetId { get; init; }

    public string? IncidentId { get; init; }

    public string? ReferenceId { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? DetailRef { get; init; }

    public string? DetailHash { get; init; }

    public string? ExcerptTail { get; init; }

    public int OriginalSummaryLength { get; init; }

    public bool SummaryTruncated { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
