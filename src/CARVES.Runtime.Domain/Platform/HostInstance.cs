namespace Carves.Runtime.Domain.Platform;

public sealed class HostInstance
{
    public int SchemaVersion { get; init; } = 1;

    public string HostId { get; init; } = string.Empty;

    public string MachineId { get; init; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public HostInstanceStatus Status { get; set; } = HostInstanceStatus.Unknown;

    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch(DateTimeOffset observedAt)
    {
        LastSeen = observedAt;
        UpdatedAt = observedAt;
    }
}
