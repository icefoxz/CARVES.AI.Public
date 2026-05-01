namespace Carves.Runtime.Domain.Platform;

public enum WorkerBackendHealthState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unavailable = 3,
    Disabled = 4,
}
