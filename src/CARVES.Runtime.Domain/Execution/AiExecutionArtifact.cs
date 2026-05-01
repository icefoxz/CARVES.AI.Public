using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class AiExecutionArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public AiExecutionRecord Record { get; init; } = new();

    public PromptSafeProjection Projection { get; init; } = new();
}
