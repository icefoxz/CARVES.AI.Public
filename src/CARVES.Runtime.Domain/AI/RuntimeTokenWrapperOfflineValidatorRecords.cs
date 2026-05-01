namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenWrapperOfflineValidatorResult
{
    public string SchemaVersion { get; init; } = "runtime-token-wrapper-offline-validator-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string ManifestMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManifestJsonArtifactPath { get; init; } = string.Empty;

    public string InventoryMarkdownArtifactPath { get; init; } = string.Empty;

    public string InventoryJsonArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectMarkdownArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool Phase11WrapperValidatorMayReferenceThisLine { get; init; }

    public string Phase10Decision { get; init; } = "insufficient_data";

    public string Phase10NextTrack { get; init; } = "insufficient_data";

    public string ValidationMode { get; init; } = "source_echo_baseline";

    public string ValidatorVerdict { get; init; } = "insufficient_data";

    public bool Phase12WrapperCandidateMayStart { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool ActiveCanaryAllowed { get; init; }

    public IReadOnlyList<string> RequestKindsCovered { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BaselineTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenWrapperOfflineValidationSurfaceResult> SurfaceResults { get; init; } = Array.Empty<RuntimeTokenWrapperOfflineValidationSurfaceResult>();

    public IReadOnlyList<RuntimeTokenWrapperOfflineManualReviewItem> ManualReviewQueue { get; init; } = Array.Empty<RuntimeTokenWrapperOfflineManualReviewItem>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperOfflineValidationSurfaceResult
{
    public string ManifestId { get; init; } = string.Empty;

    public string InventoryId { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public int SampleCount { get; init; }

    public double SourceTokensP95 { get; init; }

    public double CandidateTokensP95 { get; init; }

    public double TokenDeltaP95 { get; init; }

    public double SourceShareP95 { get; init; }

    public double CandidateShareP95 { get; init; }

    public bool SchemaValidityPass { get; init; }

    public string InvariantCoverageStatus { get; init; } = "blocked";

    public string SemanticPreservationStatus { get; init; } = "blocked";

    public string SaliencePreservationStatus { get; init; } = "blocked";

    public string PriorityPreservationStatus { get; init; } = "blocked";

    public string ComparisonMode { get; init; } = "source_echo_baseline";

    public string CandidateStrategy { get; init; } = string.Empty;

    public int ManualReviewQueueCount { get; init; }

    public IReadOnlyList<string> RequiredValidatorOutputs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperOfflineManualReviewItem
{
    public string ReviewId { get; init; } = string.Empty;

    public string ManifestId { get; init; } = string.Empty;

    public string InventoryId { get; init; } = string.Empty;

    public string InvariantId { get; init; } = string.Empty;

    public string InvariantClass { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ReviewStatus { get; init; } = "pending_candidate_diff";

    public bool BlocksPhase11Completion { get; init; }

    public bool BlocksPhase12Signoff { get; init; } = true;

    public string BlockingGate { get; init; } = "phase12_candidate_signoff";

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
