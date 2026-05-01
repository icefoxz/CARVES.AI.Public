namespace Carves.Runtime.Domain.Platform;

public sealed class RepoPolicy
{
    public string ProfileId { get; init; } = string.Empty;

    public int MaxPlannerRounds { get; init; }

    public int MaxGeneratedTasks { get; init; }

    public int MaxConcurrentExecutions { get; init; }

    public int RuntimeSelectionPriority { get; init; }

    public int StarvationWindowMinutes { get; init; }

    public bool AllowAutonomousRefactor { get; init; }

    public bool AllowAutonomousMemoryUpdate { get; init; }

    public bool ManualApprovalMode { get; init; }

    public string ProviderPolicyProfile { get; init; } = string.Empty;

    public string WorkerPolicyProfile { get; init; } = string.Empty;

    public string PreferredTrustProfileId { get; init; } = string.Empty;

    public string ReviewPolicyProfile { get; init; } = string.Empty;
}
