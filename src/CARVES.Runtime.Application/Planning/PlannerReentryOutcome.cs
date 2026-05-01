namespace Carves.Runtime.Application.Planning;

public enum PlannerReentryOutcome
{
    DeferredReviewBoundary,
    ExistingGovernedWork,
    SuggestedExecutionWork,
    SuggestedPlanningWork,
    NoJustifiedGap,
}
