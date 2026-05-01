namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenWrapperCandidateResult
{
    public string SchemaVersion { get; init; } = "runtime-token-wrapper-candidate-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string OfflineValidatorMarkdownArtifactPath { get; init; } = string.Empty;

    public string OfflineValidatorJsonArtifactPath { get; init; } = string.Empty;

    public string ManifestMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManifestJsonArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool Phase12WrapperCandidateMayReferenceThisLine { get; init; }

    public string Phase10Decision { get; init; } = "insufficient_data";

    public string Phase10NextTrack { get; init; } = "insufficient_data";

    public string CandidateSurfaceId { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CandidateMode { get; init; } = "structural_projection";

    public string CandidateSourceComponentPath { get; init; } = string.Empty;

    public string CandidateSourceAnchor { get; init; } = string.Empty;

    public double SourceTokensP50 { get; init; }

    public double SourceTokensP95 { get; init; }

    public double CandidateTokensP50 { get; init; }

    public double CandidateTokensP95 { get; init; }

    public double TokenDeltaP50 { get; init; }

    public double TokenDeltaP95 { get; init; }

    public double ReductionRatioP50 { get; init; }

    public double ReductionRatioP95 { get; init; }

    public bool MaterialReductionPass { get; init; }

    public bool SchemaValidityPass { get; init; }

    public bool InvariantCoveragePass { get; init; }

    public bool SemanticPreservationPass { get; init; }

    public bool SaliencePreservationPass { get; init; }

    public bool PriorityPreservationPass { get; init; }

    public bool EnterActiveCanaryReviewBundleReady { get; init; }

    public bool ActiveCanaryApprovalGranted { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public IReadOnlyList<string> BaselineTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenWrapperCandidateSampleResult> Samples { get; init; } = Array.Empty<RuntimeTokenWrapperCandidateSampleResult>();

    public string CandidateTextPreview { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeTokenWrapperCandidateManualReviewItem> ManualReviewQueue { get; init; } = Array.Empty<RuntimeTokenWrapperCandidateManualReviewItem>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperCandidateSampleResult
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string RequestId { get; init; } = string.Empty;

    public int SourceTokens { get; init; }

    public int CandidateTokens { get; init; }

    public int TokenDelta { get; init; }

    public double ReductionRatio { get; init; }

    public int StopConditionCount { get; init; }

    public bool SourceGroundingIncluded { get; init; }

    public bool SchemaValidityPass { get; init; }

    public bool InvariantCoveragePass { get; init; }

    public bool SemanticPreservationPass { get; init; }

    public bool SaliencePreservationPass { get; init; }

    public bool PriorityPreservationPass { get; init; }
}

public sealed record RuntimeTokenWrapperCandidateManualReviewItem
{
    public string ReviewId { get; init; } = string.Empty;

    public string ManifestId { get; init; } = string.Empty;

    public string InvariantId { get; init; } = string.Empty;

    public string InvariantClass { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ReviewStatus { get; init; } = "ready_for_operator_review_before_canary";

    public bool BlocksPhase12Completion { get; init; }

    public bool BlocksEnterActiveCanary { get; init; } = true;

    public string BlockingGate { get; init; } = "enter_active_canary_review";

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperEnterActiveCanaryReviewBundle
{
    public string SchemaVersion { get; init; } = "runtime-token-wrapper-enter-active-canary-review-bundle.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string CandidateSurfaceId { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public bool EnterActiveCanaryReviewBundleReady { get; init; }

    public bool ActiveCanaryApprovalGranted { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public double ReductionRatioP95 { get; init; }

    public bool MaterialReductionPass { get; init; }

    public bool SchemaValidityPass { get; init; }

    public bool InvariantCoveragePass { get; init; }

    public bool SemanticPreservationPass { get; init; }

    public bool SaliencePreservationPass { get; init; }

    public bool PriorityPreservationPass { get; init; }

    public IReadOnlyList<RuntimeTokenWrapperCandidateManualReviewItem> ManualReviewQueue { get; init; } = Array.Empty<RuntimeTokenWrapperCandidateManualReviewItem>();

    public IReadOnlyList<string> ReviewerChecklist { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();
}
