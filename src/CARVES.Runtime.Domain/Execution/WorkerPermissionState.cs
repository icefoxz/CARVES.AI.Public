namespace Carves.Runtime.Domain.Execution;

public enum WorkerPermissionState
{
    Pending = 0,
    Allowed = 1,
    Denied = 2,
    TimedOut = 3,
}
