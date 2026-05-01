namespace Carves.Runtime.Application.Workers;

public static class WorkerLeasePolicy
{
    public static readonly TimeSpan ExecutionIsolationBudget = TimeSpan.FromMinutes(20);

    public static readonly TimeSpan LeaseRecoveryGrace = TimeSpan.FromMinutes(5);

    public static TimeSpan DefaultLeaseDuration => ExecutionIsolationBudget + LeaseRecoveryGrace;
}
