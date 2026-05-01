namespace Carves.Runtime.Domain.Planning;

public sealed class AcceptanceContract
{
    public string ContractId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public AcceptanceContractLifecycleStatus Status { get; init; } = AcceptanceContractLifecycleStatus.Draft;

    public string Owner { get; init; } = "planner";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public AcceptanceContractIntent Intent { get; init; } = new();

    public IReadOnlyList<AcceptanceContractExample> AcceptanceExamples { get; init; } = Array.Empty<AcceptanceContractExample>();

    public AcceptanceContractChecks Checks { get; init; } = new();

    public AcceptanceContractConstraintSet Constraints { get; init; } = new();

    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    public bool AutoCompleteAllowed { get; init; }

    public IReadOnlyList<AcceptanceContractEvidenceRequirement> EvidenceRequired { get; init; } = Array.Empty<AcceptanceContractEvidenceRequirement>();

    public AcceptanceContractHumanReviewPolicy HumanReview { get; init; } = new();

    public AcceptanceContractTraceability Traceability { get; init; } = new();
}

public sealed class AcceptanceContractIntent
{
    public string Goal { get; init; } = string.Empty;

    public string BusinessValue { get; init; } = string.Empty;
}

public sealed class AcceptanceContractExample
{
    public string Given { get; init; } = string.Empty;

    public string When { get; init; } = string.Empty;

    public string Then { get; init; } = string.Empty;
}

public sealed class AcceptanceContractChecks
{
    public IReadOnlyList<string> UnitTests { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> IntegrationTests { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RegressionTests { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PolicyChecks { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AdditionalChecks { get; init; } = Array.Empty<string>();
}

public sealed class AcceptanceContractConstraintSet
{
    public IReadOnlyList<string> MustNot { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Architecture { get; init; } = Array.Empty<string>();

    public AcceptanceContractScopeLimit? ScopeLimit { get; init; }
}

public sealed class AcceptanceContractScopeLimit
{
    public int? MaxFilesChanged { get; init; }

    public int? MaxLinesChanged { get; init; }
}

public sealed class AcceptanceContractEvidenceRequirement
{
    public string Type { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class AcceptanceContractHumanReviewPolicy
{
    public bool Required { get; init; } = true;

    public bool ProvisionalAllowed { get; init; }

    public IReadOnlyList<AcceptanceContractHumanDecision> Decisions { get; init; } =
    [
        AcceptanceContractHumanDecision.Accept,
        AcceptanceContractHumanDecision.Reject,
        AcceptanceContractHumanDecision.Reopen,
    ];
}

public sealed class AcceptanceContractTraceability
{
    public string? SourceCardId { get; init; }

    public string? SourceTaskId { get; init; }

    public IReadOnlyList<string> DerivedTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RelatedArtifacts { get; init; } = Array.Empty<string>();
}

public enum AcceptanceContractLifecycleStatus
{
    Draft = 0,
    Compiled = 1,
    Red = 2,
    PartialGreen = 3,
    Green = 4,
    HumanReview = 5,
    Accepted = 6,
    ProvisionalAccepted = 7,
    Rejected = 8,
    Reopened = 9,
}

public enum AcceptanceContractHumanDecision
{
    Accept = 0,
    ProvisionalAccept = 1,
    Reject = 2,
    Reopen = 3,
}
