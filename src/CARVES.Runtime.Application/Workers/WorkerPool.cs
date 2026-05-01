using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerPool
{
    private readonly int maxWorkers;

    public WorkerPool(int maxWorkers)
    {
        this.maxWorkers = Math.Max(1, maxWorkers);
    }

    public int MaxWorkers => maxWorkers;

    public WorkerPoolSnapshot Snapshot(RuntimeSessionState session)
    {
        return new WorkerPoolSnapshot(session.ActiveWorkerCount, maxWorkers, session.ActiveTaskIds);
    }

    public WorkerLease Acquire(RuntimeSessionState session, string taskId)
    {
        var snapshot = Snapshot(session);
        if (!snapshot.HasCapacity)
        {
            return WorkerLease.Failure($"Worker pool is at capacity ({snapshot.ActiveWorkers}/{snapshot.MaxWorkers}).");
        }

        if (snapshot.ActiveTaskIds.Contains(taskId, StringComparer.Ordinal))
        {
            return WorkerLease.Failure($"Task {taskId} is already leased to an active worker.");
        }

        session.AcquireWorker(taskId);
        return WorkerLease.Success("session-inline", taskId, "local-inline", session.AttachedRepoRoot, DateTimeOffset.UtcNow);
    }

    public void Release(RuntimeSessionState session, WorkerLease lease)
    {
        if (!lease.Acquired || string.IsNullOrWhiteSpace(lease.TaskId))
        {
            return;
        }

        session.ReleaseWorker(lease.TaskId);
    }
}
