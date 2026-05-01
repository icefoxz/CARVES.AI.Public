namespace Carves.Runtime.Domain.Tasks;

public enum TaskStatus
{
    Suggested,
    Pending,
    Deferred,
    Running,
    Testing,
    Review,
    ApprovalWait,
    Completed,
    Merged,
    Failed,
    Blocked,
    Discarded,
    Superseded,
}
