namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenWrapperPolicyInvariantManifestResult
{
    public string SchemaVersion { get; init; } = "runtime-token-wrapper-policy-invariant-manifest.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string InventoryMarkdownArtifactPath { get; init; } = string.Empty;

    public string InventoryJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool Phase11WrapperInvariantManifestMayReferenceThisLine { get; init; }

    public string Phase10Decision { get; init; } = "insufficient_data";

    public string Phase10NextTrack { get; init; } = "insufficient_data";

    public string RequiredNextGate { get; init; } = "wrapper_offline_validator";

    public bool Phase12WrapperCandidateAllowed { get; init; }

    public IReadOnlyList<string> RequestKindsCovered { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CoverageLimitations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenWrapperPolicyInvariantSurfaceManifest> SurfaceManifests { get; init; } = Array.Empty<RuntimeTokenWrapperPolicyInvariantSurfaceManifest>();

    public IReadOnlyList<string> GlobalForbiddenTransforms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidatorOutputs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperPolicyInvariantSurfaceManifest
{
    public string ManifestId { get; init; } = string.Empty;

    public string InventoryId { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string SegmentKind { get; init; } = string.Empty;

    public string PayloadPath { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string SerializationKind { get; init; } = string.Empty;

    public string Producer { get; init; } = string.Empty;

    public string SourceComponentPath { get; init; } = string.Empty;

    public string SourceAnchor { get; init; } = string.Empty;

    public double ShareP95 { get; init; }

    public double TokensP95 { get; init; }

    public bool PolicyCritical { get; init; }

    public bool ManualReviewRequired { get; init; }

    public bool SemanticPreservationRequired { get; init; } = true;

    public bool SaliencePreservationRequired { get; init; } = true;

    public bool PriorityPreservationRequired { get; init; } = true;

    public string CompressionAllowed { get; init; } = "structural_only";

    public string RecommendedInventoryAction { get; init; } = string.Empty;

    public string RecommendedCandidateStrategy { get; init; } = string.Empty;

    public IReadOnlyList<string> ForbiddenTransforms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidatorChecks { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenWrapperPolicyInvariantItem> Invariants { get; init; } = Array.Empty<RuntimeTokenWrapperPolicyInvariantItem>();
}

public sealed record RuntimeTokenWrapperPolicyInvariantItem
{
    public string InvariantId { get; init; } = string.Empty;

    public string InvariantClass { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string SourceSegmentKind { get; init; } = string.Empty;

    public string SourcePayloadPath { get; init; } = string.Empty;

    public string SourceClauseSummary { get; init; } = string.Empty;

    public bool SemanticPreservationRequired { get; init; } = true;

    public bool SaliencePreservationRequired { get; init; } = true;

    public bool PriorityPreservationRequired { get; init; } = true;

    public string CompressionAllowed { get; init; } = "structural_only";

    public bool ManualReviewRequired { get; init; } = true;

    public IReadOnlyList<string> ForbiddenTransforms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidatorChecks { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
