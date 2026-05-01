namespace Carves.Runtime.Domain.Execution;

public enum WorkerPermissionAuditEventKind
{
    RequestObserved = 0,
    PolicyAllowed = 1,
    PolicyDenied = 2,
    EscalatedForReview = 3,
    HumanAllowed = 4,
    HumanDenied = 5,
    TimedOut = 6,
    ReturnedToDispatchable = 7,
}
