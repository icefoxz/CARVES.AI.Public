namespace Carves.Runtime.Domain.Runtime;

public enum RuntimeSessionStatus
{
    Idle,
    Scheduling,
    Executing,
    ReviewWait,
    ApprovalWait,
    Paused,
    Failed,
    Stopped,
}
