namespace Carves.Runtime.Domain.Platform;

public sealed class GovernanceEvent
{
    public int SchemaVersion { get; init; } = 1;

    public string EventId { get; init; } = string.Empty;

    public GovernanceEventType EventType { get; init; } = GovernanceEventType.RuntimeStarted;

    public string RepoId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
