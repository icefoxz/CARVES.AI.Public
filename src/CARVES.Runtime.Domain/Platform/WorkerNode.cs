namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerNode
{
    public int SchemaVersion { get; init; } = 1;

    public string NodeId { get; init; } = string.Empty;

    public WorkerNodeCapabilities Capabilities { get; init; } = new(true, false, false, false, 1, Array.Empty<string>());

    public WorkerNodeStatus Status { get; set; } = WorkerNodeStatus.Healthy;

    public int ActiveLeaseCount { get; set; }

    public string? LastReason { get; set; }

    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch(string? reason = null)
    {
        LastReason = reason;
        LastHeartbeatAt = DateTimeOffset.UtcNow;
    }
}
