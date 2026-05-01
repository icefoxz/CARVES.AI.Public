namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerWakeSignal
{
    public string SignalId { get; init; } = $"planner-wake-{Guid.NewGuid():N}";

    public PlannerWakeReason WakeReason { get; init; } = PlannerWakeReason.None;

    public PlannerWakeSourceKind SourceKind { get; init; } = PlannerWakeSourceKind.None;

    public string Detail { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}
