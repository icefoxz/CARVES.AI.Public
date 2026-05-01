using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenOutcomeBinding
{
    public string SchemaVersion { get; init; } = "runtime-token-outcome-binding.v2";

    public RuntimeTokenBaselineCohortFreeze Cohort { get; init; } = new();

    public RuntimeTokenTaskCostScopeSummary TaskCostScope { get; init; } = new();

    public int IncludedRequestCount { get; init; }

    public int ExcludedRequestCount { get; init; }

    public int UnboundIncludedRequestCount { get; init; }

    public int UnboundIncludedMandatoryRequestCount { get; init; }

    public int UnboundIncludedOptionalRequestCount { get; init; }

    public long UnboundIncludedContextTokens { get; init; }

    public long UnboundIncludedBillableTokens { get; init; }

    public int AttemptedTaskCount { get; init; }

    public int SuccessfulTaskCount { get; init; }

    public bool TaskCostViewTrusted { get; init; }

    public IReadOnlyList<string> TaskCostViewBlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenTaskOutcomeBreakdown> TaskOutcomeBreakdown { get; init; } = Array.Empty<RuntimeTokenTaskOutcomeBreakdown>();

    public IReadOnlyList<RuntimeTokenRequestKindInclusionSummary> RequestKindInclusion { get; init; } = Array.Empty<RuntimeTokenRequestKindInclusionSummary>();

    public IReadOnlyList<RuntimeTokenRequestBindingDispositionSummary> RequestBindingDispositionBreakdown { get; init; } = Array.Empty<RuntimeTokenRequestBindingDispositionSummary>();

    public IReadOnlyList<RuntimeTokenOperatorReadbackInclusionRecord> OperatorReadbackInclusions { get; init; } = Array.Empty<RuntimeTokenOperatorReadbackInclusionRecord>();

    public IReadOnlyList<RuntimeTokenBindingGapSummary> BindingGaps { get; init; } = Array.Empty<RuntimeTokenBindingGapSummary>();

    public RuntimeTokenRunReportCoverageSummary RunReportCoverage { get; init; } = new();

    public RuntimeTokenTaskCostViewSummary ContextWindowView { get; init; } = new();

    public RuntimeTokenTaskCostViewSummary BillableCostView { get; init; } = new();

    public IReadOnlyList<RuntimeTokenTaskOutcomeCostRecord> TaskCosts { get; init; } = Array.Empty<RuntimeTokenTaskOutcomeCostRecord>();
}

public sealed record RuntimeTokenTaskCostScopeSummary
{
    public IReadOnlyList<string> IncludedByDefault { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> ConditionallyIncluded { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> ExcludedByDefault { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenTaskOutcomeBreakdown
{
    public DomainTaskStatus TaskStatus { get; init; } = DomainTaskStatus.Pending;

    public bool Successful { get; init; }

    public int TaskCount { get; init; }
}

public sealed record RuntimeTokenRequestKindInclusionSummary
{
    public string RequestKind { get; init; } = string.Empty;

    public string Policy { get; init; } = string.Empty;

    public int IncludedRequestCount { get; init; }

    public int ExcludedRequestCount { get; init; }

    public IReadOnlyList<RuntimeTokenCountBreakdown> ExclusionReasons { get; init; } = Array.Empty<RuntimeTokenCountBreakdown>();
}

public sealed record RuntimeTokenBindingGapSummary
{
    public string Reason { get; init; } = string.Empty;

    public bool Mandatory { get; init; }

    public int RequestCount { get; init; }

    public long ContextTokens { get; init; }

    public long BillableTokens { get; init; }
}

public sealed record RuntimeTokenRequestBindingDispositionSummary
{
    public string Disposition { get; init; } = string.Empty;

    public bool Mandatory { get; init; }

    public int RequestCount { get; init; }

    public long ContextTokens { get; init; }

    public long BillableTokens { get; init; }
}

public sealed record RuntimeTokenOperatorReadbackInclusionRecord
{
    public string RequestId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? TaskId { get; init; }

    public string? ParentRequestId { get; init; }

    public bool Included { get; init; }

    public string? ReinjectionEvidenceType { get; init; }

    public string? ReinjectionRequestId { get; init; }

    public int LlmReinjectionCount { get; init; }

    public string? ExclusionReason { get; init; }
}

public sealed record RuntimeTokenRunReportCoverageSummary
{
    public int IncludedRequestsWithRunId { get; init; }

    public int IncludedRequestsWithMatchingRunReport { get; init; }

    public int IncludedRequestsMissingMatchingRunReport { get; init; }
}

public sealed record RuntimeTokenTaskCostViewSummary
{
    public string ViewId { get; init; } = string.Empty;

    public int IncludedRequestCount { get; init; }

    public int AttemptedTaskCount { get; init; }

    public int SuccessfulTaskCount { get; init; }

    public long TotalInputTokens { get; init; }

    public long TotalCachedInputTokens { get; init; }

    public long TotalOutputTokens { get; init; }

    public long TotalReasoningTokens { get; init; }

    public long TotalTokens { get; init; }

    public double? TokensPerSuccessfulTask { get; init; }
}

public sealed record RuntimeTokenTaskOutcomeCostRecord
{
    public string TaskId { get; init; } = string.Empty;

    public DomainTaskStatus TaskStatus { get; init; } = DomainTaskStatus.Pending;

    public bool Successful { get; init; }

    public int IncludedRequestCount { get; init; }

    public int IncludedRunCount { get; init; }

    public int MatchingRunReportCount { get; init; }

    public ExecutionRunStatus? LatestRunStatus { get; init; }

    public long ContextWindowTotalTokens { get; init; }

    public long BillableTotalTokens { get; init; }
}
