using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class RepoRuntimeProjection
{
    public int SchemaVersion { get; init; } = 1;

    public ProjectionTruthSource TruthSource { get; set; } = ProjectionTruthSource.PlatformProjection;

    public ProjectionFreshness Freshness { get; set; } = ProjectionFreshness.Unavailable;

    public ProjectionReconciliationOutcome LastReconciliationOutcome { get; set; } = ProjectionReconciliationOutcome.None;

    public string RefreshRule { get; set; } = "refresh_on_list_or_inspect_and_reconcile_from_repo_truth";

    public string? FreshnessReason { get; set; }

    public string? SummaryFingerprint { get; set; }

    public DateTimeOffset? ProjectedAt { get; set; }

    public DateTimeOffset? RepoObservedAt { get; set; }

    public DateTimeOffset? LastReconciledAt { get; set; }

    public string Stage { get; set; } = string.Empty;

    public RuntimeSessionStatus? SessionStatus { get; set; }

    public RuntimeActionability Actionability { get; set; } = RuntimeActionability.None;

    public int OpenOpportunityCount { get; set; }

    public int OpenTaskCount { get; set; }

    public int ReviewTaskCount { get; set; }

    public string? StopReason { get; set; }

    public string? WaitingReason { get; set; }

    public string? ActiveSessionId { get; set; }
}
