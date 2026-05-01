namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase3ReplacementScopeFreezeResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase3-replacement-scope-freeze.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewMarkdownArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementScope ReplacementScope { get; init; } = new();

    public RuntimeTokenPhase3MainPathReplacementControls Controls { get; init; } = new();

    public string FreezeVerdict { get; init; } = "scope_freeze_blocked";

    public bool ImplementationScopeFrozen { get; init; }

    public bool LimitedMainPathImplementationAllowed { get; init; }

    public bool ScopeExpansionAllowed { get; init; }

    public bool MainRendererReplacementAllowed { get; init; }

    public bool RuntimeShadowExecutionAllowed { get; init; }

    public bool FullRolloutAllowed { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
