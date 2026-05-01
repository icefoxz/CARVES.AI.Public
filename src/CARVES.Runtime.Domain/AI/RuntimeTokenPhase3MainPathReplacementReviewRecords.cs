namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase3MainPathReplacementReviewResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase3-main-path-replacement-review.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceInputs { get; init; } = Array.Empty<string>();

    public string ExecutionApprovalMarkdownArtifactPath { get; init; } = string.Empty;

    public string ExecutionApprovalJsonArtifactPath { get; init; } = string.Empty;

    public string CanaryResultMarkdownArtifactPath { get; init; } = string.Empty;

    public string CanaryResultJsonArtifactPath { get; init; } = string.Empty;

    public string CanaryResultReviewMarkdownArtifactPath { get; init; } = string.Empty;

    public string CanaryResultReviewJsonArtifactPath { get; init; } = string.Empty;

    public string PostCanaryGateMarkdownArtifactPath { get; init; } = string.Empty;

    public string PostCanaryGateJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementScope ReplacementScope { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementControls Controls { get; init; } = new();

    public string ReviewVerdict { get; init; } = "require_more_evidence";

    public bool MainPathReplacementAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool FullRolloutAllowed { get; init; }

    public bool CostSavingProven { get; init; }

    public bool NonInferiorityPassed { get; init; }

    public RuntimeTokenPhase2ActiveCanaryTokenMetrics TokenMetrics { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanaryNonInferiorityResult NonInferiority { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanarySafetyResult Safety { get; init; } = new();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase3MainPathReplacementScope
{
    public string RequestKind { get; init; } = string.Empty;

    public string Surface { get; init; } = string.Empty;

    public string ExecutionMode { get; init; } = "no_provider_agent_mediated";

    public string WorkerBackend { get; init; } = "null_worker";

    public string ProviderSdkMode { get; init; } = "not_applicable";
}

public sealed record RuntimeTokenPhase3MainPathReplacementControls
{
    public bool GlobalKillSwitchRetained { get; init; }

    public bool PerRequestKindFallbackRetained { get; init; }

    public bool PerSurfaceFallbackRetained { get; init; }

    public bool CandidateVersionPinned { get; init; }

    public bool PostRolloutAuditRequired { get; init; }

    public bool DefaultEnabledToday { get; init; }

    public string FallbackVersion { get; init; } = string.Empty;
}
