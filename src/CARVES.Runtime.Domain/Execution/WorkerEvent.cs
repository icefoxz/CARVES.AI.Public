using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerEvent
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string EventId { get; init; } = $"worker-event-{Guid.NewGuid():N}";

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public WorkerEventType EventType { get; init; } = WorkerEventType.RawError;

    public string Summary { get; init; } = string.Empty;

    public string? ItemType { get; init; }

    public string? CommandText { get; init; }

    public string? FilePath { get; init; }

    public int? ExitCode { get; init; }

    public string? RawPayload { get; init; }

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
