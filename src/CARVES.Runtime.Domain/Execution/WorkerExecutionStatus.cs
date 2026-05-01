namespace Carves.Runtime.Domain.Execution;

public enum WorkerExecutionStatus
{
    Succeeded = 0,
    Failed = 1,
    Blocked = 2,
    Skipped = 3,
    TimedOut = 4,
    Cancelled = 5,
    Aborted = 6,
    ApprovalWait = 7,
}
