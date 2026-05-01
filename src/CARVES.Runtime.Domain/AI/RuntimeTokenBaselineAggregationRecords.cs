namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineCohortFreeze
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-cohort-freeze.v1";

    public string CohortId { get; init; } = string.Empty;

    public DateTimeOffset WindowStartUtc { get; init; }

    public DateTimeOffset WindowEndUtc { get; init; }

    public IReadOnlyList<string> RequestKinds { get; init; } = Array.Empty<string>();

    public string TokenAccountingSourcePolicy { get; init; } = string.Empty;

    public string ContextWindowView { get; init; } = "context_window_input_tokens_total";

    public string BillableCostView { get; init; } = "billable_input_tokens_uncached";
}

public sealed record RuntimeTokenBaselineAggregation
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-aggregation.v2";

    public RuntimeTokenBaselineCohortFreeze Cohort { get; init; } = new();

    public int RequestCount { get; init; }

    public int UniqueTaskCount { get; init; }

    public IReadOnlyList<RuntimeTokenRequestKindBreakdown> RequestKindBreakdown { get; init; } = Array.Empty<RuntimeTokenRequestKindBreakdown>();

    public IReadOnlyList<RuntimeTokenSegmentShareSummary> SegmentKindShares { get; init; } = Array.Empty<RuntimeTokenSegmentShareSummary>();

    public RuntimeTokenBucketShareGroup ContextPackVersusNonContextPack { get; init; } = new();

    public RuntimeTokenBucketShareGroup StableVersusDynamic { get; init; } = new();

    public IReadOnlyList<RuntimeTokenTrimmedContributorSummary> TopTrimmedContributors { get; init; } = Array.Empty<RuntimeTokenTrimmedContributorSummary>();

    public RuntimeTokenAttributionQualitySummary AttributionQuality { get; init; } = new();

    public RuntimeTokenMassLedgerCoverageSummary MassLedgerCoverage { get; init; } = new();

    public RuntimeTokenCapTruthSummary CapTruth { get; init; } = new();

    public RuntimeTokenViewSummary ContextWindowView { get; init; } = new();

    public RuntimeTokenViewSummary BillableCostView { get; init; } = new();
}

public sealed record RuntimeTokenRequestKindBreakdown
{
    public string RequestKind { get; init; } = string.Empty;

    public int RequestCount { get; init; }

    public int UniqueTaskCount { get; init; }
}

public sealed record RuntimeTokenSegmentShareSummary
{
    public string SegmentKind { get; init; } = string.Empty;

    public int RequestCountWithSegment { get; init; }

    public double P50ShareRatio { get; init; }

    public double P95ShareRatio { get; init; }

    public double P50ContextWindowContributionTokens { get; init; }

    public double P95ContextWindowContributionTokens { get; init; }

    public double P50BillableContributionTokens { get; init; }

    public double P95BillableContributionTokens { get; init; }
}

public sealed record RuntimeTokenBucketShareGroup
{
    public string SummaryId { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeTokenBucketShareSummary> Buckets { get; init; } = Array.Empty<RuntimeTokenBucketShareSummary>();
}

public sealed record RuntimeTokenBucketShareSummary
{
    public string BucketId { get; init; } = string.Empty;

    public double P50ShareRatio { get; init; }

    public double P95ShareRatio { get; init; }

    public double P50ContributionTokens { get; init; }

    public double P95ContributionTokens { get; init; }

    public double P50BillableContributionTokens { get; init; }

    public double P95BillableContributionTokens { get; init; }
}

public sealed record RuntimeTokenTrimmedContributorSummary
{
    public string SegmentKind { get; init; } = string.Empty;

    public int RequestCountWithTrim { get; init; }

    public double TotalTrimmedTokensEst { get; init; }

    public double P95TrimmedTokensEst { get; init; }
}

public sealed record RuntimeTokenAttributionQualitySummary
{
    public int RequestCount { get; init; }

    public double P50UnattributedTokensEst { get; init; }

    public double P95UnattributedTokensEst { get; init; }

    public double P95UnattributedShareRatio { get; init; }

    public IReadOnlyList<RuntimeTokenCountBreakdown> TokenAccountingSourceBreakdown { get; init; } = Array.Empty<RuntimeTokenCountBreakdown>();

    public IReadOnlyList<RuntimeTokenCountBreakdown> KnownProviderOverheadBreakdown { get; init; } = Array.Empty<RuntimeTokenCountBreakdown>();

    public double P50AbsoluteProviderInputDelta { get; init; }

    public double P95AbsoluteProviderInputDelta { get; init; }
}

public sealed record RuntimeTokenMassLedgerCoverageSummary
{
    public int RequestCount { get; init; }

    public double P50ExplicitSegmentCoverageRatio { get; init; }

    public double P95ExplicitSegmentCoverageRatio { get; init; }

    public double P50ClassifiedSegmentCoverageRatio { get; init; }

    public double P95ClassifiedSegmentCoverageRatio { get; init; }

    public double P50ParentResidualShareRatio { get; init; }

    public double P95ParentResidualShareRatio { get; init; }

    public double P50KnownProviderOverheadShareRatio { get; init; }

    public double P95KnownProviderOverheadShareRatio { get; init; }

    public double P50UnknownUnattributedShareRatio { get; init; }

    public double P95UnknownUnattributedShareRatio { get; init; }
}

public sealed record RuntimeTokenCapTruthSummary
{
    public int RequestCount { get; init; }

    public int RequestsWithDirectCapTruth { get; init; }

    public int ProviderContextCapHitCount { get; init; }

    public int InternalPromptBudgetCapHitCount { get; init; }

    public int SectionBudgetCapHitCount { get; init; }

    public int TrimLoopCapHitCount { get; init; }

    public IReadOnlyList<RuntimeTokenCountBreakdown> CapTriggerSegmentKindBreakdown { get; init; } = Array.Empty<RuntimeTokenCountBreakdown>();

    public IReadOnlyList<RuntimeTokenCountBreakdown> CapTriggerSourceBreakdown { get; init; } = Array.Empty<RuntimeTokenCountBreakdown>();
}

public sealed record RuntimeTokenCountBreakdown
{
    public string Key { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed record RuntimeTokenViewSummary
{
    public string ViewId { get; init; } = string.Empty;

    public int RequestCount { get; init; }

    public double P50Tokens { get; init; }

    public double P95Tokens { get; init; }

    public double AverageTokens { get; init; }
}
