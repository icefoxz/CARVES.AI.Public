using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Execution;

[JsonConverter(typeof(JsonStringEnumConverter<BoundaryWritebackDecision>))]
public enum BoundaryWritebackDecision
{
    AdmitToWriteback,
    AdmitToReview,
    RejectResult,
    QuarantineResult,
    RequireHumanReview,
    RetryableInfraFailure,
    SemanticFailure,
}

public sealed record BoundaryDecision
{
    public string SchemaVersion { get; init; } = "boundary.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string EvidenceStatus { get; init; } = "missing";

    public string SafetyStatus { get; init; } = "not_evaluated";

    public string TestStatus { get; init; } = "not_run";

    public string FailureLane { get; init; } = "none";

    public BoundaryWritebackDecision WritebackDecision { get; init; } = BoundaryWritebackDecision.RejectResult;

    public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();

    public bool ReviewerRequired { get; init; }

    public double DecisionConfidence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string? RecommendedNextAction { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
