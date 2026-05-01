namespace Carves.Runtime.Domain.Tasks;

public enum ReviewDecisionStatus
{
    PendingReview,
    Approved,
    ProvisionalAccepted,
    Blocked,
    Superseded,
    Rejected,
    Reopened,
    NeedsAttention,
}
