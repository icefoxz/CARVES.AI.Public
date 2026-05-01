namespace Carves.Runtime.Domain.Planning;

public enum PlannerEscalationReason
{
    None,
    InvalidProposal,
    AdapterFailure,
    ProviderQuotaDenied,
    GovernanceHold,
    ReviewBoundary,
}
