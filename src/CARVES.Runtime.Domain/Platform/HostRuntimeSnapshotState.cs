namespace Carves.Runtime.Domain.Platform;

public enum HostRuntimeSnapshotState
{
    None = 0,
    Live = 1,
    Stopped = 2,
    Stale = 3,
}
