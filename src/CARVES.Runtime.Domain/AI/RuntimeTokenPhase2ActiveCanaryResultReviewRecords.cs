namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2ActiveCanaryResultReviewResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-active-canary-result-review.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string ExecutionApprovalMarkdownArtifactPath { get; init; } = string.Empty;

    public string ExecutionApprovalJsonArtifactPath { get; init; } = string.Empty;

    public string CanaryResultMarkdownArtifactPath { get; init; } = string.Empty;

    public string CanaryResultJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public RuntimeTokenPhase2ActiveCanaryScope CanaryScope { get; init; } = new();

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public string ObservationMode { get; init; } = "controlled_worker_request_path_replay";

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public string ReviewVerdict { get; init; } = "inconclusive";

    public string CanaryResultDecision { get; init; } = "inconclusive";

    public bool CanaryExecutionAuthorized { get; init; }

    public bool CostSavingObserved { get; init; }

    public bool CostSavingProven { get; init; }

    public bool NonInferiorityPassed { get; init; }

    public bool MainPathReplacementReviewEligible { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public bool FullRolloutAllowed { get; init; }

    public RuntimeTokenPhase2ActiveCanaryTokenMetrics TokenMetrics { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanaryNonInferiorityResult NonInferiority { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanarySafetyResult Safety { get; init; } = new();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
