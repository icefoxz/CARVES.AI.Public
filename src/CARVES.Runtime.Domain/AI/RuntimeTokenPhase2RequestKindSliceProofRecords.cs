namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2RequestKindSliceProofResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-request-kind-slice-proof.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string ManifestMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManifestJsonArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CrossKindProofVerdict { get; init; } = "proof_not_available";

    public bool CrossKindProofAvailable { get; init; }

    public IReadOnlyList<string> CanaryRequestKindAllowlist { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequestKindsReviewed { get; init; } = Array.Empty<string>();

    public int PolicyCriticalFragmentCount { get; init; }

    public int PolicyCriticalFragmentRemovedCount { get; init; }

    public IReadOnlyList<RuntimeTokenPhase2RequestKindSliceProofMatrixEntry> MatrixEntries { get; init; } = Array.Empty<RuntimeTokenPhase2RequestKindSliceProofMatrixEntry>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase2RequestKindSliceProofMatrixEntry
{
    public string FragmentId { get; init; } = string.Empty;

    public string FragmentTitle { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string MatrixStatus { get; init; } = string.Empty;

    public bool PolicyCritical { get; init; } = true;

    public bool RemovedFromRequestKind { get; init; }

    public bool CandidateScope { get; init; }

    public string ManualReviewStatus { get; init; } = string.Empty;

    public string Evidence { get; init; } = string.Empty;

    public bool Blocking { get; init; }
}
