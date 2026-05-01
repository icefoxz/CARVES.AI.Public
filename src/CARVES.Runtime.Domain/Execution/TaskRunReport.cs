using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class TaskRunReport
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string TaskId { get; init; } = string.Empty;

    public WorkerRequest Request { get; init; } = new();

    public ExecutionSession Session { get; init; } = new(string.Empty, string.Empty, string.Empty, false, string.Empty, string.Empty, string.Empty, DateTimeOffset.UtcNow);

    public string? WorktreePath { get; init; }

    public ValidationResult Validation { get; init; } = new();

    public PatchSummary Patch { get; init; } = PatchSummary.Empty;

    public WorkerExecutionResult WorkerExecution { get; init; } = WorkerExecutionResult.None;

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public bool DryRun { get; init; }

    public string? ResultCommit { get; init; }

    public SafetyDecision SafetyDecision { get; init; } = SafetyDecision.Allow(string.Empty);
}
