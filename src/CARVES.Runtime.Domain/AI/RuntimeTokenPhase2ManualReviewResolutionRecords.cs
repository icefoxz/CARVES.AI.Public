namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2ManualReviewResolutionResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-manual-review-resolution.v1";

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

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string ResolutionVerdict { get; init; } = "unresolved";

    public int ResolvedReviewCount { get; init; }

    public int UnresolvedReviewCount { get; init; }

    public int FailCount { get; init; }

    public int CandidateChangeRequiredCount { get; init; }

    public int SemanticPreservationPassCount { get; init; }

    public int SemanticPreservationFailCount { get; init; }

    public int SaliencePreservationPassCount { get; init; }

    public int SaliencePreservationFailCount { get; init; }

    public int PriorityPreservationPassCount { get; init; }

    public int PriorityPreservationFailCount { get; init; }

    public int ApplicabilityPassCount { get; init; }

    public int ApplicabilityFailCount { get; init; }

    public IReadOnlyList<RuntimeTokenPhase2ManualReviewResolutionItem> ReviewItems { get; init; } = Array.Empty<RuntimeTokenPhase2ManualReviewResolutionItem>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase2ManualReviewResolutionItem
{
    public string ReviewItemId { get; init; } = string.Empty;

    public string InvariantId { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string IssueType { get; init; } = string.Empty;

    public string ReviewResult { get; init; } = "unresolved";

    public string SemanticReviewResult { get; init; } = "unresolved";

    public string SalienceReviewResult { get; init; } = "unresolved";

    public string PriorityReviewResult { get; init; } = "unresolved";

    public string ApplicabilityReviewResult { get; init; } = "unresolved";

    public string ReviewerRationale { get; init; } = string.Empty;

    public bool CandidateChangeRequired { get; init; }

    public bool Blocking { get; init; } = true;

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
