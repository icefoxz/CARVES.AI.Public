namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerBackendHealthSummary
{
    public WorkerBackendHealthState State { get; init; } = WorkerBackendHealthState.Unknown;

    public string Summary { get; init; } = string.Empty;

    public long? LatencyMs { get; init; }

    public string? DegradationReason { get; init; }

    public int ConsecutiveFailureCount { get; init; }

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
}
