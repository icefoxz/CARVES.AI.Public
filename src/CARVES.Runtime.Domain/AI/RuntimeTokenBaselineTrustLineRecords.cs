namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineTrustLineResult
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-trust-line-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool SupersedesPreLedgerLine { get; init; }

    public string EvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string EvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string ReadinessMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReadinessJsonArtifactPath { get; init; } = string.Empty;

    public string RecomputeMarkdownArtifactPath { get; init; } = string.Empty;

    public string RecomputeJsonArtifactPath { get; init; } = string.Empty;

    public string RecommendationDecision { get; init; } = "insufficient_data";

    public string RecommendationNextTrack { get; init; } = "insufficient_data";

    public bool Phase10TargetDecisionMayReferenceThisLine { get; init; }

    public bool CapBasedTargetDecisionAllowed { get; init; }

    public bool TotalCostClaimAllowed { get; init; }

    public bool Phase12TargetedCompactCandidateAllowed { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool ActiveCanaryAllowed { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
