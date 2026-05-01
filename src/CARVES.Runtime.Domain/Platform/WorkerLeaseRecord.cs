namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerLeaseRecord
{
    public int SchemaVersion { get; init; } = 1;

    public string LeaseId { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string? RepoId { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public WorkerLeaseStatus Status { get; set; } = WorkerLeaseStatus.Active;

    public WorkerLeaseDisposition OnExpiry { get; set; } = WorkerLeaseDisposition.ReturnToDispatchable;

    public DateTimeOffset AcquiredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? CompletionReason { get; set; }

    public void Renew(DateTimeOffset now, TimeSpan leaseDuration, string? reason = null)
    {
        LastHeartbeatAt = now;
        ExpiresAt = now.Add(leaseDuration);
        CompletionReason = reason ?? CompletionReason;
    }

    public void Complete(WorkerLeaseStatus status, string reason, DateTimeOffset now)
    {
        Status = status;
        CompletionReason = reason;
        CompletedAt = now;
    }
}
