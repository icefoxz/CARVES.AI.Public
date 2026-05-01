using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorktreeRuntimeRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string RecordId { get; init; } = $"worktree-runtime-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string WorktreePath { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string BaseCommit { get; init; } = string.Empty;

    public WorktreeRuntimeState State { get; set; } = WorktreeRuntimeState.Active;

    public string? QuarantineReason { get; set; }

    public string? RebuiltFromWorktreePath { get; init; }

    public string? WorkerRunId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
