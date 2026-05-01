using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public static class PlannerLifecycleSemantics
{
    public static string DescribeState(PlannerLifecycleState state)
    {
        return state switch
        {
            PlannerLifecycleState.Active => "active",
            PlannerLifecycleState.Sleeping => "sleeping",
            PlannerLifecycleState.Waiting => "waiting",
            PlannerLifecycleState.Blocked => "blocked",
            PlannerLifecycleState.Escalated => "escalated",
            _ => "idle",
        };
    }

    public static string DescribeWakeReason(PlannerWakeReason reason)
    {
        return reason switch
        {
            PlannerWakeReason.NewGoalArrived => "new goal arrived",
            PlannerWakeReason.NewCardArrived => "new card arrived",
            PlannerWakeReason.ExecutionBacklogCleared => "execution backlog cleared",
            PlannerWakeReason.OpportunityDeltaDetected => "opportunity delta detected",
            PlannerWakeReason.WorkerResultReturned => "worker result returned",
            PlannerWakeReason.ApprovalResolved => "approval resolved",
            PlannerWakeReason.TaskFailed => "task failed",
            PlannerWakeReason.DependencyUnlocked => "dependency unlocked",
            PlannerWakeReason.ExplicitHumanWake => "explicit human wake",
            _ => "(none)",
        };
    }

    public static string DescribeSleepReason(PlannerSleepReason reason)
    {
        return reason switch
        {
            PlannerSleepReason.NoVisibleGoal => "no visible goal",
            PlannerSleepReason.AllTasksBlockedByDependencies => "all tasks blocked by dependencies",
            PlannerSleepReason.WaitingForWorkerResults => "waiting for worker results",
            PlannerSleepReason.WaitingForReview => "waiting for review",
            PlannerSleepReason.WaitingForHumanAction => "waiting for human action",
            PlannerSleepReason.NoOpenOpportunities => "no open opportunities",
            PlannerSleepReason.ExistingGovernedWork => "existing governed work",
            PlannerSleepReason.AutonomyLimitReached => "planner autonomy limit reached",
            _ => "(none)",
        };
    }

    public static string DescribeEscalationReason(PlannerEscalationReason reason)
    {
        return reason switch
        {
            PlannerEscalationReason.InvalidProposal => "invalid proposal",
            PlannerEscalationReason.AdapterFailure => "planner adapter failure",
            PlannerEscalationReason.ProviderQuotaDenied => "provider quota denied",
            PlannerEscalationReason.GovernanceHold => "governance hold",
            PlannerEscalationReason.ReviewBoundary => "review boundary",
            _ => "(none)",
        };
    }
}
