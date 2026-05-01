using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerExecutionArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public WorkerExecutionResult Result { get; init; } = WorkerExecutionResult.None;

    public ExecutionEvidence Evidence { get; init; } = ExecutionEvidence.None;

    public PromptSafeProjection Projection { get; init; } = new();
}
