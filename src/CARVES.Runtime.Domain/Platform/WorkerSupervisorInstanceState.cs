namespace Carves.Runtime.Domain.Platform;

public enum WorkerSupervisorInstanceState
{
    Requested,
    Starting,
    Running,
    Stopping,
    Stopped,
    Lost,
    Failed,
}
