namespace Carves.Runtime.Domain.Platform;

public enum DelegatedRunLifecycleState
{
    None = 0,
    Running = 1,
    Stalled = 2,
    Expired = 3,
    Orphaned = 4,
    Completed = 5,
    Quarantined = 6,
    Retryable = 7,
    ManualReviewRequired = 8,
    Blocked = 9,
}
