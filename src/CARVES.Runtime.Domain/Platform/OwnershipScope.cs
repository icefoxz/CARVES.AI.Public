namespace Carves.Runtime.Domain.Platform;

public enum OwnershipScope
{
    TaskMutation = 0,
    WorkerInterruption = 1,
    ApprovalDecision = 2,
    PlannerControl = 3,
    RuntimeControl = 4,
}
