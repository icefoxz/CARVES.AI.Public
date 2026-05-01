using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public enum OperatorActionabilityState
{
    AdvisoryOnly = 0,
    AutonomousDegraded = 1,
    HumanRequired = 2,
    Blocked = 3,
}

public enum OperatorActionabilityReason
{
    None = 0,
    PendingApproval = 1,
    PendingReview = 2,
    ActiveBlocker = 3,
    ProviderUnavailable = 4,
    ProviderDegraded = 5,
    SessionPause = 6,
    SessionTerminal = 7,
    NonBlockingNoise = 8,
    AutonomousExecution = 9,
    NoSession = 10,
    ProviderOptionalResidue = 11,
}

public sealed class OperatorActionabilityAssessment
{
    public OperatorActionabilityState State { get; init; } = OperatorActionabilityState.AdvisoryOnly;

    public OperatorActionabilityReason Reason { get; init; } = OperatorActionabilityReason.None;

    public RuntimeActionability SessionActionability { get; init; } = RuntimeActionability.None;

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public int HealthyProviderCount { get; init; }

    public int DegradedProviderCount { get; init; }

    public int UnavailableProviderCount { get; init; }

    public int ActionableProviderIssueCount { get; init; }

    public int OptionalProviderIssueCount { get; init; }

    public int DisabledProviderCount { get; init; }

    public string? SelectedBackendId { get; init; }

    public string? PreferredBackendId { get; init; }

    public bool FallbackInUse { get; init; }

    public bool RequiresHuman => State == OperatorActionabilityState.HumanRequired || State == OperatorActionabilityState.Blocked;
}
