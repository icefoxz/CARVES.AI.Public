namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2ActiveCanaryApprovalReviewResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-active-canary-approval-review.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string ReadinessReviewMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReadinessReviewJsonArtifactPath { get; init; } = string.Empty;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReviewBundleJsonArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionMarkdownArtifactPath { get; init; } = string.Empty;

    public string ManualReviewResolutionJsonArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofMarkdownArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofJsonArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanMarkdownArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanJsonArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortMarkdownArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortJsonArtifactPath { get; init; } = string.Empty;

    public string ApprovalRequested { get; init; } = "active_canary";

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public double TargetSurfaceReductionRatioP95 { get; init; }

    public double TargetSurfaceShareP95 { get; init; }

    public double ExpectedWholeRequestReductionP95 { get; init; }

    public bool DefaultEnabled { get; init; }

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public IReadOnlyList<string> CanaryRequestKindAllowlist { get; init; } = Array.Empty<string>();

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public bool RollbackPlanFrozen { get; init; }

    public bool NonInferiorityCohortFrozen { get; init; }

    public string ReviewVerdict { get; init; } = "blocked_for_active_canary";

    public bool PrerequisiteReviewPassed { get; init; }

    public bool CanaryImplementationAuthorized { get; init; }

    public bool CanaryExecutionAuthorized { get; init; }

    public bool ActiveCanaryApproved { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExecutionNotApprovedReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
