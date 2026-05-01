namespace Carves.Runtime.Domain.Platform;

public enum WorkerLeaseStatus
{
    Active,
    Released,
    Expired,
    Quarantined,
}
