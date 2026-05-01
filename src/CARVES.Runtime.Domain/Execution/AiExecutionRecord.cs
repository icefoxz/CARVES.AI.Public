using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed record AiExecutionRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string Provider { get; init; } = string.Empty;

    public string WorkerAdapter { get; init; } = string.Empty;

    public string WorkerAdapterReason { get; init; } = string.Empty;

    public string? ProtocolFamily { get; init; }

    public string? RequestFamily { get; init; }

    public string Model { get; init; } = string.Empty;

    public bool Configured { get; init; }

    public bool Succeeded { get; init; }

    public bool UsedFallback { get; init; }

    public string? FallbackProvider { get; init; }

    public string? RequestId { get; init; }

    public string RequestPreview { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public string ResponsePreview { get; init; } = string.Empty;

    public string? ResponseHash { get; init; }

    public string? OutputText { get; init; }

    public string? FailureReason { get; init; }

    public WorkerFailureLayer FailureLayer { get; init; } = WorkerFailureLayer.None;

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public static AiExecutionRecord Skipped(string provider, string model, string reason, string requestPreview, string requestHash)
    {
        return new AiExecutionRecord
        {
            Provider = provider,
            WorkerAdapter = provider,
            WorkerAdapterReason = reason,
            Model = model,
            Configured = false,
            Succeeded = true,
            RequestPreview = requestPreview,
            RequestHash = requestHash,
            ResponsePreview = reason,
            OutputText = reason,
        };
    }
}
