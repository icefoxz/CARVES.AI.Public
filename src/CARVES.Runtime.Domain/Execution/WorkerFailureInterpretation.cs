namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerFailureInterpretation
{
    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.Unknown;

    public bool Retryable { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RawEvidence { get; init; } = string.Empty;
}
