namespace Carves.Runtime.Domain.Platform;

public enum HostControlState
{
    Running,
    Paused,
    Stopped,
}

public enum HostControlAction
{
    Started,
    PauseRequested,
    ResumeRequested,
    TerminateRequested,
}
