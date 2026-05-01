using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Execution;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionBudgetSize>))]
public enum ExecutionBudgetSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionConfidenceLevel>))]
public enum ExecutionConfidenceLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionRiskLevel>))]
public enum ExecutionRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionChangeKind>))]
public enum ExecutionChangeKind
{
    SourceCode = 0,
    Tests = 1,
    Documentation = 2,
    Configuration = 3,
    ControlPlaneState = 4,
    Contracts = 5,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionBoundaryDecision>))]
public enum ExecutionBoundaryDecision
{
    Allow = 0,
    Review = 1,
    Block = 2,
    Stop = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionBoundaryStopReason>))]
public enum ExecutionBoundaryStopReason
{
    SizeExceeded = 0,
    RetryExceeded = 1,
    UnstableExecution = 2,
    Timeout = 3,
    ScopeViolation = 4,
    RiskExceeded = 5,
    ManagedWorkspaceHostOnlyPath = 6,
    ManagedWorkspaceDeniedPath = 7,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionBoundaryReplanStrategy>))]
public enum ExecutionBoundaryReplanStrategy
{
    SplitTask = 0,
    NarrowScope = 1,
    RetryWithReducedBudget = 2,
}

public sealed record ExecutionConfidence
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public int TotalRuns { get; init; }

    public int SuccessCount { get; init; }

    public int FailureStreak { get; init; }

    public double SuccessRate { get; init; }

    public bool HasRecentFailures { get; init; }

    public ExecutionConfidenceLevel Level { get; init; } = ExecutionConfidenceLevel.Medium;

    public string Rationale { get; init; } = string.Empty;
}

public sealed record ExecutionBudget
{
    public string SchemaVersion { get; init; } = "1.0";

    public ExecutionBudgetSize Size { get; init; } = ExecutionBudgetSize.Small;

    public ExecutionConfidenceLevel ConfidenceLevel { get; init; } = ExecutionConfidenceLevel.Medium;

    public int MaxFiles { get; init; }

    public int MaxLinesChanged { get; init; }

    public int MaxRetries { get; init; } = 1;

    public double MaxFailureDensity { get; init; } = 1.0;

    public int MaxDurationMinutes { get; init; }

    public bool RequiresReviewBoundary { get; init; }

    public IReadOnlyList<ExecutionChangeKind> ChangeKinds { get; init; } = Array.Empty<ExecutionChangeKind>();

    public string Summary { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;
}

public sealed record ExecutionTelemetry
{
    public string SchemaVersion { get; init; } = "1.0";

    public int FilesChanged { get; init; }

    public int LinesChanged { get; init; }

    public int RetryCount { get; init; }

    public int FailureCount { get; init; }

    public double FailureDensity { get; init; }

    public int DurationSeconds { get; init; }

    public IReadOnlyList<string> ObservedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ExecutionChangeKind> ChangeKinds { get; init; } = Array.Empty<ExecutionChangeKind>();

    public bool BudgetExceeded { get; init; }

    public string? Summary { get; init; }
}

public sealed record ExecutionBoundaryAssessment
{
    public ExecutionBudget Budget { get; init; } = new();

    public ExecutionConfidence Confidence { get; init; } = new();

    public ExecutionTelemetry Telemetry { get; init; } = new();

    public ExecutionRiskLevel RiskLevel { get; init; } = ExecutionRiskLevel.Low;

    public int RiskScore { get; init; }

    public ExecutionBoundaryDecision Decision { get; init; } = ExecutionBoundaryDecision.Allow;

    public bool ShouldStop { get; init; }

    public ExecutionBoundaryStopReason? StopReason { get; init; }

    public string StopDetail { get; init; } = string.Empty;

    public ManagedWorkspacePathPolicyAssessment ManagedWorkspacePathPolicy { get; init; } = new();

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

public sealed record ExecutionBoundaryBudgetSnapshot
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public int CurrentStepIndex { get; init; }

    public int TotalSteps { get; init; }

    public ExecutionBudget Budget { get; init; } = new();

    public ExecutionConfidence Confidence { get; init; } = new();

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExecutionBoundaryTelemetrySnapshot
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public int CurrentStepIndex { get; init; }

    public int TotalSteps { get; init; }

    public ExecutionTelemetry Telemetry { get; init; } = new();

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExecutionBoundaryViolation
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public int StoppedAtStep { get; init; }

    public int TotalSteps { get; init; }

    public ExecutionBoundaryStopReason Reason { get; init; }

    public string Detail { get; init; } = string.Empty;

    public ExecutionRiskLevel RiskLevel { get; init; } = ExecutionRiskLevel.Low;

    public int RiskScore { get; init; }

    public ExecutionBudget Budget { get; init; } = new();

    public ExecutionTelemetry Telemetry { get; init; } = new();

    public ExecutionConfidence Confidence { get; init; } = new();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExecutionBoundaryReplanConstraints
{
    public int MaxFiles { get; init; }

    public int MaxLinesChanged { get; init; }

    public IReadOnlyList<ExecutionChangeKind> AllowedChangeKinds { get; init; } = Array.Empty<ExecutionChangeKind>();
}

public sealed record ExecutionBoundaryReplanRequest
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public int StoppedAtStep { get; init; }

    public int TotalSteps { get; init; }

    public string? RunGoal { get; init; }

    public ExecutionBoundaryReplanStrategy Strategy { get; init; } = ExecutionBoundaryReplanStrategy.NarrowScope;

    public ExecutionBoundaryStopReason ViolationReason { get; init; }

    public string ViolationPath { get; init; } = string.Empty;

    public ExecutionBoundaryReplanConstraints Constraints { get; init; } = new();

    public IReadOnlyList<string> FollowUpSuggestions { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
