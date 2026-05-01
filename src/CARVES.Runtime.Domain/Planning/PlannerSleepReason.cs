namespace Carves.Runtime.Domain.Planning;

public enum PlannerSleepReason
{
    None,
    NoVisibleGoal,
    AllTasksBlockedByDependencies,
    WaitingForWorkerResults,
    WaitingForReview,
    WaitingForHumanAction,
    NoOpenOpportunities,
    ExistingGovernedWork,
    AutonomyLimitReached,
}
