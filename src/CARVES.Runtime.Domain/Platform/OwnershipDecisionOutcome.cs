namespace Carves.Runtime.Domain.Platform;

public enum OwnershipDecisionOutcome
{
    Granted = 0,
    Denied = 1,
    Deferred = 2,
    Escalated = 3,
}
