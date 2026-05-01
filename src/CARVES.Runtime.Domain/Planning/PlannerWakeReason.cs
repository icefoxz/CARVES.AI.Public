namespace Carves.Runtime.Domain.Planning;

public enum PlannerWakeReason
{
    None,
    NewGoalArrived,
    NewCardArrived,
    ExecutionBacklogCleared,
    OpportunityDeltaDetected,
    WorkerResultReturned,
    ApprovalResolved,
    TaskFailed,
    DependencyUnlocked,
    ExplicitHumanWake,
}
