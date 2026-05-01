namespace Carves.Runtime.Domain.Execution;

public sealed record WorkerFailureClassification
{
    public WorkerFailureKind Kind { get; init; } = WorkerFailureKind.Unknown;

    public WorkerFailureLane Lane { get; init; } = WorkerFailureLane.Unknown;

    public bool Retryable { get; init; }

    public bool ReplanAllowed { get; init; }

    public string ReasonCode { get; init; } = "unknown_failure";

    public string? SubstrateCategory { get; init; }

    public string TaskStatusRecommendation { get; init; } = "review";

    public string NextAction { get; init; } = "review failure evidence";
}
