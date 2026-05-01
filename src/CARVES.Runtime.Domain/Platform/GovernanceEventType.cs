namespace Carves.Runtime.Domain.Platform;

public enum GovernanceEventType
{
    RuntimeStarted,
    RuntimePaused,
    RuntimeStopped,
    ProviderRotated,
    ProviderQuotaDenied,
    ProviderFallbackUsed,
    WorkerQuarantined,
    WorkerLeaseExpired,
    WorkerTrustedProfileActivated,
    WorkerTrustedProfileDenied,
    WorkerFailureEscalated,
    WorkerPermissionEscalated,
    WorkerPermissionAllowed,
    WorkerPermissionDenied,
    WorkerPermissionTimedOut,
    AutonomyLimitReached,
    RepoRegistered,
}
