namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2ActiveCanaryReadinessReviewResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-active-canary-readiness-review.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleJsonArtifactPath { get; init; } = string.Empty;

    public string ManifestMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManifestJsonArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionJsonArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofMarkdownArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofJsonArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanMarkdownArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanJsonArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortMarkdownArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public string Phase10Decision { get; init; } = "insufficient_data";

    public string Phase10NextTrack { get; init; } = "insufficient_data";

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string ReviewVerdict { get; init; } = "blocked_before_review";

    public bool EnterActiveCanaryReviewAccepted { get; init; }

    public bool ActiveCanaryApproved { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public double TargetSurfaceReductionRatioP95 { get; init; }

    public double TargetSurfaceShareP95 { get; init; }

    public double ExpectedWholeRequestReductionP95 { get; init; }

    public int PolicyInvariantCount { get; init; }

    public int PolicyInvariantCoverageCount { get; init; }

    public double PolicyInvariantCoverageRatio { get; init; }

    public int SemanticPreservationFailCount { get; init; }

    public int SaliencePreservationFailCount { get; init; }

    public int PriorityPreservationFailCount { get; init; }

    public int NeedsManualReviewUnresolvedCount { get; init; }

    public int RequestKindSliceRemovedPolicyCriticalCount { get; init; }

    public bool RequestKindSliceCrossKindProofAvailable { get; init; }

    public bool RuntimePathTouched { get; init; }

    public bool RetrievalOrEvidenceWritten { get; init; }

    public bool NonInferiorityCohortFrozen { get; init; }

    public bool RollbackPlanReviewed { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredBeforeActiveCanary { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
