using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerBroker
{
    private readonly WorkerPool workerPool;
    private readonly IWorkerNodeRegistryRepository repository;
    private readonly IWorkerLeaseRepository leaseRepository;
    private readonly IRepoRuntimeGateway runtimeGateway;
    private readonly TimeSpan leaseDuration;

    public WorkerBroker(
        WorkerPool workerPool,
        IWorkerNodeRegistryRepository repository,
        IWorkerLeaseRepository leaseRepository,
        IRepoRuntimeGateway runtimeGateway,
        TimeSpan? leaseDuration = null)
    {
        this.workerPool = workerPool;
        this.repository = repository;
        this.leaseRepository = leaseRepository;
        this.runtimeGateway = runtimeGateway;
        this.leaseDuration = leaseDuration ?? WorkerLeasePolicy.DefaultLeaseDuration;
    }

    public WorkerPoolSnapshot Snapshot(RuntimeSessionState session)
    {
        EnsureLocalNode();
        return workerPool.Snapshot(session);
    }

    public WorkerLease Acquire(RuntimeSessionState session, string taskId)
    {
        ExpireStaleLeases();
        var nodes = EnsureLocalNode();
        var node = nodes.FirstOrDefault(candidate => candidate.Status != WorkerNodeStatus.Quarantined && candidate.ActiveLeaseCount < candidate.Capabilities.MaxConcurrentTasks);
        if (node is null)
        {
            return WorkerLease.Failure("No healthy worker node is available.");
        }

        var lease = workerPool.Acquire(session, taskId);
        if (!lease.Acquired)
        {
            return lease;
        }

        var leaseRecord = new WorkerLeaseRecord
        {
            LeaseId = $"lease-{Guid.NewGuid():N}",
            NodeId = node.NodeId,
            RepoPath = session.AttachedRepoRoot,
            SessionId = session.SessionId,
            TaskId = taskId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(leaseDuration),
        };
        SaveLeases(
            leaseRepository.Load()
                .Where(existing => !string.Equals(existing.LeaseId, leaseRecord.LeaseId, StringComparison.Ordinal))
                .Append(leaseRecord)
                .ToArray());
        node.ActiveLeaseCount += 1;
        node.Status = WorkerNodeStatus.Busy;
        node.Touch($"Leased task {taskId}.");
        SaveNodes(nodes);
        return WorkerLease.Success(leaseRecord.LeaseId, taskId, node.NodeId, session.AttachedRepoRoot, leaseRecord.ExpiresAt);
    }

    public void Release(RuntimeSessionState session, WorkerLease lease)
    {
        workerPool.Release(session, lease);
        if (!lease.Acquired || string.IsNullOrWhiteSpace(lease.NodeId) || string.IsNullOrWhiteSpace(lease.LeaseId))
        {
            return;
        }

        var leases = leaseRepository.Load().ToList();
        var leaseRecord = leases.FirstOrDefault(existing => string.Equals(existing.LeaseId, lease.LeaseId, StringComparison.Ordinal));
        leaseRecord?.Complete(WorkerLeaseStatus.Released, $"Released task {lease.TaskId}.", DateTimeOffset.UtcNow);
        SaveLeases(leases);

        var nodes = EnsureLocalNode();
        var node = nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, lease.NodeId, StringComparison.Ordinal));
        if (node is null)
        {
            return;
        }

        node.ActiveLeaseCount = Math.Max(0, node.ActiveLeaseCount - 1);
        if (node.Status != WorkerNodeStatus.Quarantined)
        {
            node.Status = node.ActiveLeaseCount == 0 ? WorkerNodeStatus.Healthy : WorkerNodeStatus.Busy;
        }

        node.Touch($"Released task {lease.TaskId}.");
        SaveNodes(nodes);
    }

    public IReadOnlyList<WorkerNode> ListNodes()
    {
        ExpireStaleLeases();
        return EnsureLocalNode().OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<WorkerLeaseRecord> ListLeases()
    {
        ExpireStaleLeases();
        return leaseRepository.Load()
            .OrderByDescending(lease => lease.AcquiredAt)
            .ThenBy(lease => lease.LeaseId, StringComparer.Ordinal)
            .ToArray();
    }

    public WorkerNode Heartbeat(string nodeId)
    {
        var nodes = EnsureLocalNode();
        var node = nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Worker node '{nodeId}' was not found.");
        node.Touch("Heartbeat received.");
        if (node.Status == WorkerNodeStatus.Offline)
        {
            node.Status = WorkerNodeStatus.Healthy;
        }

        var now = DateTimeOffset.UtcNow;
        var leases = leaseRepository.Load().ToList();
        foreach (var lease in leases.Where(existing =>
                     existing.Status == WorkerLeaseStatus.Active
                     && string.Equals(existing.NodeId, nodeId, StringComparison.Ordinal)))
        {
            lease.Renew(now, leaseDuration, "Heartbeat renewed.");
        }

        SaveLeases(leases);
        SaveNodes(nodes);
        return node;
    }

    public WorkerNode Quarantine(string nodeId, string reason)
    {
        var nodes = EnsureLocalNode();
        var node = nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Worker node '{nodeId}' was not found.");
        node.Status = WorkerNodeStatus.Quarantined;
        node.ActiveLeaseCount = 0;
        node.Touch(reason);
        CompleteNodeLeases(nodeId, WorkerLeaseStatus.Quarantined, reason);
        SaveNodes(nodes);
        return node;
    }

    public WorkerLeaseRecord ExpireLease(string leaseId, string reason)
    {
        var leases = leaseRepository.Load().ToList();
        var lease = leases.FirstOrDefault(existing => string.Equals(existing.LeaseId, leaseId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Worker lease '{leaseId}' was not found.");
        if (lease.Status == WorkerLeaseStatus.Active)
        {
            CompleteLease(lease, WorkerLeaseStatus.Expired, reason);
            SaveLeases(leases);
            ReleaseNodeCapacity(lease.NodeId, lease.TaskId, reason);
            RecoverLease(lease, reason);
        }

        return lease;
    }

    private void ExpireStaleLeases()
    {
        var now = DateTimeOffset.UtcNow;
        var leases = leaseRepository.Load().ToList();
        var stale = leases.Where(lease => lease.Status == WorkerLeaseStatus.Active && lease.ExpiresAt <= now).ToArray();
        if (stale.Length == 0)
        {
            return;
        }

        foreach (var lease in stale)
        {
            CompleteLease(lease, WorkerLeaseStatus.Expired, "Lease expired after heartbeat timeout.");
            ReleaseNodeCapacity(lease.NodeId, lease.TaskId, "Lease expired after heartbeat timeout.");
            RecoverLease(lease, "Lease expired after heartbeat timeout.");
        }

        SaveLeases(leases);
    }

    private List<WorkerNode> EnsureLocalNode()
    {
        var nodes = repository.Load().ToList();
        if (nodes.Count != 0)
        {
            return nodes;
        }

        nodes.Add(new WorkerNode
        {
            NodeId = "local-default",
            Capabilities = new WorkerNodeCapabilities(true, false, false, false, workerPool.MaxWorkers, ["*"], SupportsCodexSdk: true, SupportsTrustedAutomation: true, SupportsNetworkedAgents: true),
            Status = WorkerNodeStatus.Healthy,
        });
        SaveNodes(nodes);
        return nodes;
    }

    private void SaveNodes(IReadOnlyList<WorkerNode> nodes)
    {
        repository.Save(nodes.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray());
    }

    private void SaveLeases(IReadOnlyList<WorkerLeaseRecord> leases)
    {
        leaseRepository.Save(leases.OrderBy(lease => lease.LeaseId, StringComparer.Ordinal).ToArray());
    }

    private void CompleteNodeLeases(string nodeId, WorkerLeaseStatus status, string reason)
    {
        var leases = leaseRepository.Load().ToList();
        foreach (var lease in leases.Where(existing =>
                     existing.Status == WorkerLeaseStatus.Active
                     && string.Equals(existing.NodeId, nodeId, StringComparison.Ordinal)))
        {
            CompleteLease(lease, status, reason);
            RecoverLease(lease, reason);
        }

        SaveLeases(leases);
    }

    private void CompleteLease(WorkerLeaseRecord lease, WorkerLeaseStatus status, string reason)
    {
        lease.Complete(status, reason, DateTimeOffset.UtcNow);
    }

    private void ReleaseNodeCapacity(string nodeId, string taskId, string reason)
    {
        var nodes = EnsureLocalNode();
        var node = nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
        if (node is null)
        {
            return;
        }

        node.ActiveLeaseCount = Math.Max(0, node.ActiveLeaseCount - 1);
        if (node.Status != WorkerNodeStatus.Quarantined)
        {
            node.Status = node.ActiveLeaseCount == 0 ? WorkerNodeStatus.Healthy : WorkerNodeStatus.Busy;
        }

        node.Touch(reason);
        SaveNodes(nodes);
    }

    private void RecoverLease(WorkerLeaseRecord lease, string reason)
    {
        runtimeGateway.Execute(new RepoRuntimeCommandRequest(
            RepoRuntimeGatewayOperation.RecoverLease,
            lease.RepoPath,
            lease.RepoId,
            false,
            reason,
            lease.SessionId,
            lease.TaskId,
            lease.OnExpiry));
    }
}
