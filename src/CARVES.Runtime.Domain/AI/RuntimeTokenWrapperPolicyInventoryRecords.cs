namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenWrapperPolicyInventoryResult
{
    public string SchemaVersion { get; init; } = "runtime-token-wrapper-policy-inventory-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string EvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string EvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string TrustMarkdownArtifactPath { get; init; } = string.Empty;

    public string TrustJsonArtifactPath { get; init; } = string.Empty;

    public string Phase10MarkdownArtifactPath { get; init; } = string.Empty;

    public string Phase10JsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool Phase11WrapperInventoryMayReferenceThisLine { get; init; }

    public string Phase10Decision { get; init; } = "insufficient_data";

    public string Phase10NextTrack { get; init; } = "insufficient_data";

    public int CohortRequestCount { get; init; }

    public IReadOnlyList<string> RequestKindsCovered { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CoverageLimitations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenWrapperPolicyRequestKindSummary> RequestKindSummaries { get; init; } = Array.Empty<RuntimeTokenWrapperPolicyRequestKindSummary>();

    public IReadOnlyList<RuntimeTokenWrapperPolicySurfaceSummary> WrapperSurfaces { get; init; } = Array.Empty<RuntimeTokenWrapperPolicySurfaceSummary>();

    public IReadOnlyList<RuntimeTokenWrapperPolicySurfaceSummary> TopWrapperSurfaces { get; init; } = Array.Empty<RuntimeTokenWrapperPolicySurfaceSummary>();

    public IReadOnlyList<RuntimeTokenWrapperPolicySurfaceSummary> RepeatedBoilerplateSurfaces { get; init; } = Array.Empty<RuntimeTokenWrapperPolicySurfaceSummary>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenWrapperPolicyRequestKindSummary
{
    public string RequestKind { get; init; } = string.Empty;

    public int RequestCount { get; init; }

    public int WrapperSurfaceCount { get; init; }

    public double WrapperTokensP50 { get; init; }

    public double WrapperTokensP95 { get; init; }

    public double WrapperShareP50 { get; init; }

    public double WrapperShareP95 { get; init; }
}

public sealed record RuntimeTokenWrapperPolicySurfaceSummary
{
    public string InventoryId { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string SegmentKind { get; init; } = string.Empty;

    public string PayloadPath { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string SerializationKind { get; init; } = string.Empty;

    public string Producer { get; init; } = string.Empty;

    public int RequestCountWithSurface { get; init; }

    public double CohortFrequencyRatio { get; init; }

    public double RequestKindFrequencyRatio { get; init; }

    public double TokensP50 { get; init; }

    public double TokensP95 { get; init; }

    public double ShareP50 { get; init; }

    public double ShareP95 { get; init; }

    public int DistinctContentHashCount { get; init; }

    public bool RepeatedAcrossRequests { get; init; }

    public bool RepeatedAcrossRequestKinds { get; init; }

    public bool PolicyCritical { get; init; }

    public bool ManualReviewRequired { get; init; }

    public string BoilerplateClass { get; init; } = string.Empty;

    public string CompressionAllowed { get; init; } = string.Empty;

    public string RecommendedInventoryAction { get; init; } = string.Empty;

    public IReadOnlyList<string> SampleAttributionIds { get; init; } = Array.Empty<string>();
}
