namespace Carves.Runtime.Domain.Execution;

public sealed class WorktreeRebuildRequest
{
    public string TaskId { get; init; } = string.Empty;

    public string SourceWorktreePath { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}
