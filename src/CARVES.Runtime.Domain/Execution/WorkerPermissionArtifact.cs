using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerPermissionArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public IReadOnlyList<WorkerPermissionRequest> Requests { get; init; } = Array.Empty<WorkerPermissionRequest>();
}
