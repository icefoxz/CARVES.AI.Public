using System.Text.Json.Serialization;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Domain.Planning;

[JsonConverter(typeof(JsonStringEnumConverter<PlannerReplanTrigger>))]
public enum PlannerReplanTrigger
{
    ReviewRejected = 0,
    TaskFailed = 1,
    AcceptanceUnmet = 2,
    BoundaryStopped = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<PlannerSuggestionStatus>))]
public enum PlannerSuggestionStatus
{
    Draft = 0,
    Inserted = 1,
    Rejected = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<PlannerSuggestionGuardVerdict>))]
public enum PlannerSuggestionGuardVerdict
{
    Pass = 0,
    Warn = 1,
    Reject = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<PlanningSignalKind>))]
public enum PlanningSignalKind
{
    FailureContext = 0,
    ReviewGap = 1,
    ExecutionPattern = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<PlanningSignalSeverity>))]
public enum PlanningSignalSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ChangeSurfaceRiskLevel>))]
public enum ChangeSurfaceRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public sealed record PlannerReplanEntry
{
    public string SchemaVersion { get; init; } = "planner-replan-entry.v1";

    public string EntryId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public PlannerReplanTrigger Trigger { get; init; } = PlannerReplanTrigger.TaskFailed;

    public string EntryState { get; init; } = "replan_required";

    public string Reason { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? FailureId { get; init; }

    public string? IncidentId { get; init; }

    public string? ChangeSurfaceId { get; init; }

    public string? PlanningSignalId { get; init; }

    public IReadOnlyList<string> SuggestedTaskIds { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record StructuredIncidentRecord
{
    public string SchemaVersion { get; init; } = "structured-incident.v1";

    public string IncidentId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? FailureId { get; init; }

    public FailureType? FailureType { get; init; }

    public PlannerReplanTrigger Trigger { get; init; } = PlannerReplanTrigger.TaskFailed;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Context { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> SuspectedArea { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PatchChangeSurface
{
    public string SchemaVersion { get; init; } = "patch-change-surface.v1";

    public string SurfaceId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public int FilesChanged { get; init; }

    public int LinesChanged { get; init; }

    public IReadOnlyList<string> ModulesTouched { get; init; } = Array.Empty<string>();

    public ChangeSurfaceRiskLevel RiskLevel { get; init; } = ChangeSurfaceRiskLevel.Low;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SuggestedTaskRecord
{
    public string SchemaVersion { get; init; } = "suggested-task.v1";

    public string SuggestionId { get; init; } = string.Empty;

    public string SourceTaskId { get; init; } = string.Empty;

    public string ProposedTaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public PlannerSuggestionStatus Status { get; init; } = PlannerSuggestionStatus.Draft;

    public PlannerSuggestionGuardVerdict GuardVerdict { get; init; } = PlannerSuggestionGuardVerdict.Pass;

    public string GuardReason { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public string? IncidentId { get; init; }

    public string? PlanningSignalId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? InsertedTaskId { get; init; }

    public DateTimeOffset? InsertedAtUtc { get; init; }

    public string? ApprovalReason { get; init; }
}

public sealed record PlanningSignalRecord
{
    public string SchemaVersion { get; init; } = "planning-signal.v1";

    public string SignalId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public PlanningSignalKind Kind { get; init; } = PlanningSignalKind.FailureContext;

    public PlanningSignalSeverity Severity { get; init; } = PlanningSignalSeverity.Low;

    public string Summary { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public ExecutionPatternType? PatternType { get; init; }

    public string? FailureId { get; init; }

    public IReadOnlyList<string> RunIds { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExecutionMemoryRecord
{
    public string SchemaVersion { get; init; } = "execution-memory.v1";

    public string MemoryId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string EventKind { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? FailureId { get; init; }

    public string? ReviewVerdict { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> ArtifactPaths { get; init; } = Array.Empty<string>();

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlannerEmergenceProjection
{
    public int ReplanRequiredTaskCount { get; init; }

    public int DraftSuggestedTaskCount { get; init; }

    public int PlanningSignalCount { get; init; }

    public int ExecutionMemoryRecordCount { get; init; }
}

public sealed record SuggestedTaskInsertionResult(
    bool Allowed,
    string Message,
    SuggestedTaskRecord? Suggestion = null,
    string? InsertedTaskId = null);
