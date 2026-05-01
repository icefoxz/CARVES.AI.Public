namespace Carves.Runtime.Domain.Platform;

public sealed class PlatformPolicy
{
    public string PolicyId { get; init; } = "default-platform";

    public int MaxActiveSessions { get; init; }

    public int MaxWorkerNodes { get; init; }

    public int ProviderQuotaPerHour { get; init; }

    public bool FairSchedulingEnabled { get; init; }

    public int MaxRepoSelectionsPerTick { get; init; }

    public int StarvationPreventionMinutes { get; init; }
}
