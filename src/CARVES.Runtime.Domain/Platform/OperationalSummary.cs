namespace Carves.Runtime.Domain.Platform;

public sealed class OperationalSummary
{
    public string RepoId { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;

    public string SessionStatus { get; init; } = string.Empty;

    public string Actionability { get; init; } = string.Empty;

    public string SessionActionability { get; init; } = string.Empty;

    public string ActionabilityReason { get; init; } = string.Empty;

    public string ActionabilitySummary { get; init; } = string.Empty;

    public int WorkerCount { get; init; }

    public int ActiveWorkerCount { get; init; }

    public int RunningTaskCount { get; init; }

    public int PendingApprovalCount { get; init; }

    public int BlockedTaskCount { get; init; }

    public int ReviewTaskCount { get; init; }

    public int ActiveIncidentCount { get; init; }

    public int RecentIncidentCount { get; init; }

    public int ProjectionNoiseCount { get; init; }

    public int LegacyDebtCount { get; init; }

    public int ProviderHealthIssueCount { get; init; }

    public int OptionalProviderHealthIssueCount { get; init; }

    public int DisabledProviderCount { get; init; }

    public int PendingRebuildCount { get; init; }

    public string ProjectionWritebackState { get; init; } = "healthy";

    public string ProjectionWritebackSummary { get; init; } = "Markdown projection writeback is healthy.";

    public int ProjectionWritebackFailureCount { get; init; }

    public string? LastDelegationTaskId { get; init; }

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<OperationalQueueItem> ApprovalQueue { get; init; } = Array.Empty<OperationalQueueItem>();

    public IReadOnlyList<OperationalQueueItem> BlockedQueue { get; init; } = Array.Empty<OperationalQueueItem>();

    public IReadOnlyList<OperationalIncidentDrilldown> Incidents { get; init; } = Array.Empty<OperationalIncidentDrilldown>();

    public IReadOnlyList<OperationalRecoveryOutcome> RecoveryOutcomes { get; init; } = Array.Empty<OperationalRecoveryOutcome>();

    public IReadOnlyList<OperationalProviderHealthSummary> Providers { get; init; } = Array.Empty<OperationalProviderHealthSummary>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class OperationalQueueItem
{
    public string ItemId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;
}

public sealed class OperationalIncidentDrilldown
{
    public string IncidentId { get; init; } = string.Empty;

    public string IncidentType { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? BackendId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Consequence { get; init; } = string.Empty;

    public string RecoveryAction { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class OperationalRecoveryOutcome
{
    public string TaskId { get; init; } = string.Empty;

    public string RecoveryAction { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class OperationalProviderHealthSummary
{
    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public long? LatencyMs { get; init; }

    public int ConsecutiveFailureCount { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string SelectionRole { get; init; } = string.Empty;

    public string ActionabilityImpact { get; init; } = string.Empty;

    public bool ActionabilityRelevant { get; init; }
}

public sealed class ProviderHealthActionabilityProjection
{
    public string? SelectedBackendId { get; init; }

    public string? PreferredBackendId { get; init; }

    public bool FallbackInUse { get; init; }

    public int HealthyActionableProviderCount { get; init; }

    public int DegradedActionableProviderCount { get; init; }

    public int UnavailableActionableProviderCount { get; init; }

    public int OptionalIssueCount { get; init; }

    public int DisabledIssueCount { get; init; }

    public IReadOnlyList<OperationalProviderHealthSummary> Providers { get; init; } = Array.Empty<OperationalProviderHealthSummary>();
}
