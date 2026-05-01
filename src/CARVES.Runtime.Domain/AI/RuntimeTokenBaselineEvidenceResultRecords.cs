namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineEvidenceResult
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-evidence-result.v2";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public RuntimeTokenBaselineAggregation Aggregation { get; init; } = new();

    public RuntimeTokenOutcomeBinding OutcomeBinding { get; init; } = new();

    public RuntimeTokenHardCapTriggerAnalysis HardCapTriggerAnalysis { get; init; } = new();

    public RuntimeTokenDecisionInputsReadiness DecisionInputsReadiness { get; init; } = new();

    public RuntimeTokenPhase10DecisionInputs DecisionInputs { get; init; } = new();

    public RuntimeTokenPhase10TargetRecommendation Recommendation { get; init; } = new();
}

public sealed record RuntimeTokenHardCapTriggerAnalysis
{
    public string Status { get; init; } = "unavailable";

    public bool DirectMetricsAvailable { get; init; }

    public bool ProxyMetricsAvailable { get; init; }

    public bool CapBasedDominanceAllowed { get; init; }

    public string? PrimaryCapTriggerSegmentKind { get; init; }

    public string? PrimaryCapTriggerSource { get; init; }

    public int RequestsWithDirectCapTruth { get; init; }

    public int ProviderContextCapHitCount { get; init; }

    public int InternalPromptBudgetCapHitCount { get; init; }

    public int SectionBudgetCapHitCount { get; init; }

    public int TrimLoopCapHitCount { get; init; }

    public string? PrimaryTrimPressureSegmentKind { get; init; }

    public double? PrimaryTrimmedTokensP95 { get; init; }

    public bool UsesTrimPressureProxy { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenDecisionInputsReadiness
{
    public bool HasRequestKindBreakdown { get; init; }

    public bool HasP95SegmentShares { get; init; }

    public bool HasRendererVsNonRendererSplit { get; init; }

    public bool HasStableVsDynamicSplit { get; init; }

    public bool HasTrimPressureVisibility { get; init; }

    public bool HasAttributionQuality { get; init; }

    public bool HasContextWindowView { get; init; }

    public bool HasBillableCostView { get; init; }

    public bool HasSuccessfulTaskCostView { get; init; }

    public bool HasExplicitHardCapTriggerAnalysis { get; init; }

    public bool HasDirectHardCapTruth { get; init; }

    public bool CapBasedTargetDecisionAllowed { get; init; }

    public bool AttributionShareReady { get; init; }

    public bool TaskCostReady { get; init; }

    public bool RouteReinjectionReady { get; init; }

    public bool CapTruthReady { get; init; }

    public bool Phase10TargetDecisionAllowed { get; init; }

    public bool TotalCostClaimAllowed { get; init; }

    public bool ActiveCanaryAllowed { get; init; }

    public bool ReadyForPhase10TargetDecision { get; init; }

    public IReadOnlyList<string> AttributionShareBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TaskCostBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RouteReinjectionBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CapTruthBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ActiveCanaryBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase10DecisionInputs
{
    public double ContextPackExplicitShareP95 { get; init; }

    public double NonContextPackExplicitShareP95 { get; init; }

    public double StableExplicitShareP95 { get; init; }

    public double DynamicExplicitShareP95 { get; init; }

    public double RendererShareP95Proxy { get; init; }

    public double ToolSchemaShareP95Proxy { get; init; }

    public double WrapperPolicyShareP95Proxy { get; init; }

    public double OtherSegmentShareP95Proxy { get; init; }

    public double ParentResidualShareP95 { get; init; }

    public double KnownProviderOverheadShareP95 { get; init; }

    public double UnknownUnattributedShareP95 { get; init; }

    public IReadOnlyList<RuntimeTokenPhase10ContributorSummary> TopP95Contributors { get; init; } = Array.Empty<RuntimeTokenPhase10ContributorSummary>();

    public IReadOnlyList<RuntimeTokenPhase10TrimmedContributorSummary> TopTrimmedContributors { get; init; } = Array.Empty<RuntimeTokenPhase10TrimmedContributorSummary>();

    public IReadOnlyList<string> HardCapTriggerSegments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DominanceCriteriaSatisfied { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase10ContributorSummary
{
    public string SegmentKind { get; init; } = string.Empty;

    public string TargetSegmentClass { get; init; } = string.Empty;

    public double ShareP95 { get; init; }

    public double ContextTokensP95 { get; init; }

    public double BillableTokensP95 { get; init; }
}

public sealed record RuntimeTokenPhase10TrimmedContributorSummary
{
    public string SegmentKind { get; init; } = string.Empty;

    public string TargetSegmentClass { get; init; } = string.Empty;

    public double TrimmedTokensP95 { get; init; }

    public double TrimmedShareProxyP95 { get; init; }
}

public sealed record RuntimeTokenPhase10TargetRecommendation
{
    public string Decision { get; init; } = "insufficient_data";

    public string? TargetSegment { get; init; }

    public string? TargetSegmentClass { get; init; }

    public double? TargetShareP95 { get; init; }

    public double? TrimmedShareProxyP95 { get; init; }

    public string? HardCapTriggerSegment { get; init; }

    public IReadOnlyList<string> DominanceBasis { get; init; } = Array.Empty<string>();

    public string NextTrack { get; init; } = "insufficient_data";

    public string Confidence { get; init; } = "low";

    public IReadOnlyList<string> BlockedCriteria { get; init; } = Array.Empty<string>();
}
