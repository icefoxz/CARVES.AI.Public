namespace Carves.Runtime.Domain.Planning;

public enum PlannerProposalAcceptanceStatus
{
    PendingValidation,
    Accepted,
    PartiallyAccepted,
    Deferred,
    Rejected,
}
