namespace Carves.Runtime.Domain.Platform;

public enum ActorSessionState
{
    Active = 0,
    Idle = 1,
    Waiting = 2,
    Sleeping = 3,
    Blocked = 4,
    Stopped = 5,
}
