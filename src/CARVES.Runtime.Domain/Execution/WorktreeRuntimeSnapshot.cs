namespace Carves.Runtime.Domain.Execution;

public sealed class WorktreeRuntimeSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<WorktreeRuntimeRecord> Records { get; init; } = Array.Empty<WorktreeRuntimeRecord>();

    public IReadOnlyList<WorktreeRebuildRequest> PendingRebuilds { get; init; } = Array.Empty<WorktreeRebuildRequest>();
}
