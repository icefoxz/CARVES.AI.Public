using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderHealthRecord
{
    public int SchemaVersion { get; init; } = 1;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public WorkerBackendHealthState State { get; init; } = WorkerBackendHealthState.Unknown;

    public string Summary { get; init; } = string.Empty;

    public long? LatencyMs { get; init; }

    public string? DegradationReason { get; init; }

    public int ConsecutiveFailureCount { get; init; }

    public WorkerFailureKind LastFailureKind { get; init; } = WorkerFailureKind.None;

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
}
