using Carves.Runtime.Domain.Execution;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.Platform;

public sealed class DelegatedRunRecoveryLedgerEntry
{
    public int SchemaVersion { get; init; } = 1;

    public string RecoveryEntryId { get; init; } = $"drr-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string? RunId { get; init; }

    public string? LeaseId { get; init; }

    public WorkerLeaseStatus? LeaseStatus { get; init; }

    public WorkerRecoveryAction RecoveryAction { get; init; } = WorkerRecoveryAction.None;

    public string RecoveryReason { get; init; } = string.Empty;

    public string ActorIdentity { get; init; } = string.Empty;

    public string PolicyIdentity { get; init; } = string.Empty;

    public string LifecycleState { get; init; } = string.Empty;

    public DomainTaskStatus TaskStatusBefore { get; init; } = DomainTaskStatus.Pending;

    public DomainTaskStatus TaskStatusAfter { get; init; } = DomainTaskStatus.Pending;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DelegatedRunRecoveryLedgerSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<DelegatedRunRecoveryLedgerEntry> Entries { get; init; } = Array.Empty<DelegatedRunRecoveryLedgerEntry>();
}
