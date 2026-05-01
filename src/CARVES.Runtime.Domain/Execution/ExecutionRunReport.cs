using System.Text.Json.Serialization;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Domain.Execution;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionPatternType>))]
public enum ExecutionPatternType
{
    HealthyProgress = 0,
    RepeatedFailure = 1,
    BoundaryLoop = 2,
    ReplanLoop = 3,
    ScopeDrift = 4,
    OverExecution = 5,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionPatternSeverity>))]
public enum ExecutionPatternSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionPatternSuggestion>))]
public enum ExecutionPatternSuggestion
{
    ContinueWithinBudget = 0,
    ManualReview = 1,
    NarrowScope = 2,
    ChangeReplanStrategy = 3,
    PauseAndReview = 4,
}

public sealed record ExecutionRunReport
{
    public string SchemaVersion { get; init; } = "execution-run-report.v1";

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public ExecutionRunStatus RunStatus { get; init; } = ExecutionRunStatus.Planned;

    public ExecutionBoundaryStopReason? BoundaryReason { get; init; }

    public FailureType? FailureType { get; init; }

    public ExecutionBoundaryReplanStrategy? ReplanStrategy { get; init; }

    public IReadOnlyList<string> ModulesTouched { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ExecutionStepKind> StepKinds { get; init; } = Array.Empty<ExecutionStepKind>();

    public int FilesChanged { get; init; }

    public int CompletedSteps { get; init; }

    public int TotalSteps { get; init; }

    public RuntimePackExecutionAttribution? SelectedPack { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExecutionPatternEvidence
{
    public string RunId { get; init; } = string.Empty;

    public ExecutionRunStatus RunStatus { get; init; } = ExecutionRunStatus.Planned;

    public ExecutionBoundaryStopReason? BoundaryReason { get; init; }

    public FailureType? FailureType { get; init; }

    public ExecutionBoundaryReplanStrategy? ReplanStrategy { get; init; }

    public int FilesChanged { get; init; }

    public int CompletedSteps { get; init; }

    public int TotalSteps { get; init; }

    public IReadOnlyList<string> ModulesTouched { get; init; } = Array.Empty<string>();
}

public sealed record ExecutionPattern
{
    public string SchemaVersion { get; init; } = "execution-pattern.v1";

    public string TaskId { get; init; } = string.Empty;

    public ExecutionPatternType Type { get; init; } = ExecutionPatternType.HealthyProgress;

    public ExecutionPatternSeverity Severity { get; init; } = ExecutionPatternSeverity.Low;

    public ExecutionPatternSuggestion Suggestion { get; init; } = ExecutionPatternSuggestion.ContinueWithinBudget;

    public string Summary { get; init; } = string.Empty;

    public string Fingerprint { get; init; } = string.Empty;

    public int RunsAnalyzed { get; init; }

    public IReadOnlyList<ExecutionPatternEvidence> Evidence { get; init; } = Array.Empty<ExecutionPatternEvidence>();
}
