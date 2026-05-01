namespace Carves.Runtime.Application.Platform;

public sealed record GovernanceReport(
    DateTimeOffset GeneratedAt,
    int WindowHours,
    DateTimeOffset WindowStartedAt,
    IReadOnlyList<GovernanceDecisionCount> ApprovalDecisions,
    IReadOnlyList<GovernanceApprovalPreview> RecentApprovalEvents,
    IReadOnlyList<GovernanceProviderStabilitySummary> UnstableProviders,
    IReadOnlyList<GovernanceTaskClassCount> PermissionBlockedTaskClasses,
    int RecoverySampleCount,
    int RecoverySuccessfulCount,
    double RecoverySuccessRate,
    IReadOnlyList<GovernanceRepoIncidentDensity> RepoIncidentDensity,
    string IncompletenessNote);

public sealed record GovernanceDecisionCount(string Decision, int Count);

public sealed record GovernanceApprovalPreview(
    string PermissionRequestId,
    string RepoId,
    string TaskId,
    string ProviderId,
    string Decision,
    string Actor,
    string ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record GovernanceProviderStabilitySummary(
    string BackendId,
    string ProviderId,
    string State,
    int IncidentCount,
    int ConsecutiveFailureCount,
    long? LatencyMs,
    string Summary);

public sealed record GovernanceTaskClassCount(string TaskType, int Count);

public sealed record GovernanceRepoIncidentDensity(string RepoId, int IncidentCount);
