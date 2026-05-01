namespace Carves.Runtime.Domain.Planning;

public enum PlannerLifecycleState
{
    Idle,
    Active,
    Sleeping,
    Waiting,
    Blocked,
    Escalated,
}
