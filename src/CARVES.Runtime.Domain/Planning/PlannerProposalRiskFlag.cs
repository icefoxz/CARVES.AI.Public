namespace Carves.Runtime.Domain.Planning;

public enum PlannerProposalRiskFlag
{
    None,
    HumanReviewRequired,
    GovernanceSensitive,
    ScopeExpansion,
    InvalidTaskType,
    DependencyConflict,
    QuotaSensitive,
}
