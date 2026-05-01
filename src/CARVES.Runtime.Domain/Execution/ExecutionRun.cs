using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Execution;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionRunStatus>))]
public enum ExecutionRunStatus
{
    Planned = 0,
    Running = 1,
    Completed = 2,
    Stopped = 3,
    Failed = 4,
    Abandoned = 5,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionRunTriggerReason>))]
public enum ExecutionRunTriggerReason
{
    Initial = 0,
    Retry = 1,
    Replan = 2,
    Resume = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionStepKind>))]
public enum ExecutionStepKind
{
    Inspect = 0,
    Implement = 1,
    Verify = 2,
    Writeback = 3,
    Cleanup = 4,
    Replan = 5,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionStepStatus>))]
public enum ExecutionStepStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Failed = 4,
    Blocked = 5,
}

public sealed record ExecutionStepTemplate
{
    public string SchemaVersion { get; init; } = "execution-run-plan.v1";

    public string Title { get; init; } = string.Empty;

    public ExecutionStepKind Kind { get; init; } = ExecutionStepKind.Implement;

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ExecutionRunPlan
{
    public string SchemaVersion { get; init; } = "execution-run-plan.v1";

    public string TaskId { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public ExecutionRunTriggerReason TriggerReason { get; init; } = ExecutionRunTriggerReason.Initial;

    public IReadOnlyList<ExecutionStepTemplate> Steps { get; init; } = Array.Empty<ExecutionStepTemplate>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ExecutionStep
{
    public string StepId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public ExecutionStepKind Kind { get; init; } = ExecutionStepKind.Implement;

    public ExecutionStepStatus Status { get; init; } = ExecutionStepStatus.Pending;

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndedAtUtc { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ExecutionRun
{
    public string SchemaVersion { get; init; } = "execution-run.v1";

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public ExecutionRunStatus Status { get; init; } = ExecutionRunStatus.Planned;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? EndedAtUtc { get; init; }

    public string? PlannerContextId { get; init; }

    public ExecutionRunTriggerReason TriggerReason { get; init; } = ExecutionRunTriggerReason.Initial;

    public string Goal { get; init; } = string.Empty;

    public int CurrentStepIndex { get; init; }

    public IReadOnlyList<ExecutionStep> Steps { get; init; } = Array.Empty<ExecutionStep>();

    public string? ResultEnvelopePath { get; init; }

    public string? BoundaryViolationPath { get; init; }

    public string? ReplanArtifactPath { get; init; }

    public RuntimePackExecutionAttribution? SelectedPack { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
