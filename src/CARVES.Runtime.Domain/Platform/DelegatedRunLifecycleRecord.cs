using Carves.Runtime.Domain.Execution;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.Platform;

public sealed class DelegatedRunLifecycleRecord
{
    public int SchemaVersion { get; init; } = 1;

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string? SessionId { get; init; }

    public string? LeaseId { get; init; }

    public WorkerLeaseStatus? LeaseStatus { get; init; }

    public string? RunId { get; init; }

    public string? BackendId { get; init; }

    public string? ProviderId { get; init; }

    public string? WorktreePath { get; init; }

    public WorktreeRuntimeState? WorktreeState { get; init; }

    public DomainTaskStatus TaskStatus { get; init; } = DomainTaskStatus.Pending;

    public DelegatedRunLifecycleState State { get; init; } = DelegatedRunLifecycleState.None;

    public WorkerRecoveryAction RecoveryAction { get; init; } = WorkerRecoveryAction.None;

    public bool Retryable { get; init; }

    public bool RequiresOperatorAction { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string? LatestRecoveryEntryId { get; init; }

    public string? LatestRecoveryActorIdentity { get; init; }

    public DateTimeOffset? LatestRecoveryRecordedAt { get; init; }

    public string? RepoTruthAnchor { get; init; }

    public string? PlatformTruthAnchor { get; init; }

    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
}
