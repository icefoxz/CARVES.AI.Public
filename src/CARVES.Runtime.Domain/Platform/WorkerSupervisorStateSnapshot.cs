using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerSupervisorStateSnapshot
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public IReadOnlyList<WorkerSupervisorInstanceRecord> Entries { get; init; } = Array.Empty<WorkerSupervisorInstanceRecord>();

    public IReadOnlyList<WorkerSupervisorArchivedInstanceRecord> ArchivedEntries { get; init; } = Array.Empty<WorkerSupervisorArchivedInstanceRecord>();

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class WorkerSupervisorArchivedInstanceRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string ArchiveId { get; init; } = $"worker-supervisor-archive-{Guid.NewGuid():N}";

    public string WorkerInstanceId { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string WorkerIdentity { get; init; } = string.Empty;

    public WorkerSupervisorInstanceState PreviousState { get; init; } = WorkerSupervisorInstanceState.Requested;

    public string? ActorSessionId { get; init; }

    public string? ScheduleBinding { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset ArchivedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset OriginalCreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset OriginalUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
