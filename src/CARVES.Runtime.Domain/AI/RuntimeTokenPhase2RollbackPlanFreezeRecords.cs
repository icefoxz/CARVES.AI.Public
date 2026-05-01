namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2RollbackPlanFreezeResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-wrapper-canary-rollback-plan.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofMarkdownArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = "original";

    public bool RollbackPlanReviewed { get; init; }

    public bool RollbackTestPlanDefined { get; init; }

    public bool DefaultEnabled { get; init; }

    public bool GlobalKillSwitch { get; init; }

    public bool PerRequestKindFallback { get; init; }

    public bool PerSurfaceFallback { get; init; }

    public IReadOnlyList<string> CanaryRequestKindAllowlist { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AutomaticRollbackTriggers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ManualRollbackActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
