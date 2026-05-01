namespace Carves.Runtime.Application.Workers;

public sealed record WorkerLease(
    bool Acquired,
    string? LeaseId,
    string? TaskId,
    string? NodeId,
    string? RepoPath,
    DateTimeOffset? ExpiresAt,
    string Reason)
{
    public static WorkerLease Success(string leaseId, string taskId, string? nodeId, string repoPath, DateTimeOffset expiresAt)
    {
        return new WorkerLease(true, leaseId, taskId, nodeId, repoPath, expiresAt, "Worker slot acquired.");
    }

    public static WorkerLease Failure(string reason)
    {
        return new WorkerLease(false, null, null, null, null, null, reason);
    }
}
