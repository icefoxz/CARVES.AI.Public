using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.AI;

public sealed class ProviderProtocolResult
{
    public WorkerExecutionStatus Status { get; init; } = WorkerExecutionStatus.Failed;

    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.Unknown;

    public WorkerFailureLayer FailureLayer { get; init; } = WorkerFailureLayer.Provider;

    public bool Retryable { get; init; }

    public bool Configured { get; init; }

    public string Model { get; init; } = string.Empty;

    public string? RequestId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string? OutputText { get; init; }

    public string? FailureReason { get; init; }

    public string? RawResponse { get; init; }

    public int? HttpStatusCode { get; init; }

    public long? TransportLatencyMs { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public IReadOnlyList<WorkerEvent> Events { get; init; } = Array.Empty<WorkerEvent>();
}
