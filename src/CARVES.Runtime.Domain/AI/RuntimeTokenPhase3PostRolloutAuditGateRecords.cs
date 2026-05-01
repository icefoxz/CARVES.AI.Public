namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase3PostRolloutAuditGateResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase3-post-rollout-audit-gate.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewMarkdownArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewJsonArtifactPath { get; init; } = string.Empty;

    public string ReplacementScopeFreezeMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReplacementScopeFreezeJsonArtifactPath { get; init; } = string.Empty;

    public string PostRolloutEvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string PostRolloutEvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementScope ReplacementScope { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementControls Controls { get; init; } = new();

    public string GateVerdict { get; init; } = "blocked_pending_post_rollout_evidence";

    public bool PostRolloutAuditPassed { get; init; }

    public bool LimitedMainPathImplementationObserved { get; init; }

    public bool MainPathReplacementRetained { get; init; }

    public bool RequestKindExpansionAllowed { get; init; }

    public bool SurfaceExpansionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool FullRolloutAllowed { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
