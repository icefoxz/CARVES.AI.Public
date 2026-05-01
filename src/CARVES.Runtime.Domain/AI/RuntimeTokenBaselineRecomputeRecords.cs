namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineRecomputeResult
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-recompute-result.v2";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset RecomputedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string RecomputeMode { get; init; } = "recomputed_from_raw_records";

    public string CohortJsonArtifactPath { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string EvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string EvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string ReadinessMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReadinessJsonArtifactPath { get; init; } = string.Empty;

    public string TrustMarkdownArtifactPath { get; init; } = string.Empty;

    public string TrustJsonArtifactPath { get; init; } = string.Empty;

    public RuntimeTokenBaselineCohortFreeze Cohort { get; init; } = new();

    public string ReadinessVerdict { get; init; } = "insufficient_data";

    public bool Phase10TargetDecisionAllowed { get; init; }

    public string RecommendationDecision { get; init; } = "insufficient_data";

    public string RecommendationNextTrack { get; init; } = "insufficient_data";

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool SupersedesPreLedgerLine { get; init; }

    public bool CapBasedTargetDecisionAllowed { get; init; }

    public bool TotalCostClaimAllowed { get; init; }

    public bool AdditionalCollectionRecommended { get; init; }

    public IReadOnlyList<string> AdditionalCollectionReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
