namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineReadinessGateResult
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-readiness-gate-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string EvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string EvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string Verdict { get; init; } = "insufficient_data";

    public bool UnlocksPhase10TargetDecision { get; init; }

    public RuntimeTokenBaselineReadinessDimensions Readiness { get; init; } = new();

    public IReadOnlyList<RuntimeTokenBaselineReadinessCheck> Checks { get; init; } = Array.Empty<RuntimeTokenBaselineReadinessCheck>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenBaselineReadinessCheck
{
    public string CheckId { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public bool Blocking { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record RuntimeTokenBaselineReadinessDimensions
{
    public bool AttributionShareReady { get; init; }

    public bool TaskCostReady { get; init; }

    public bool RouteReinjectionReady { get; init; }

    public bool CapTruthReady { get; init; }

    public bool Phase10TargetDecisionAllowed { get; init; }

    public bool CapBasedTargetDecisionAllowed { get; init; }

    public bool TotalCostClaimAllowed { get; init; }

    public bool ActiveCanaryAllowed { get; init; }

    public IReadOnlyList<string> AttributionShareBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TaskCostBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RouteReinjectionBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CapTruthBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ActiveCanaryBlockingReasons { get; init; } = Array.Empty<string>();
}
