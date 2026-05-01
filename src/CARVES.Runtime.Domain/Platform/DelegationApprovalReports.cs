namespace Carves.Runtime.Domain.Platform;

public sealed record DelegationReport(
    DateTimeOffset GeneratedAt,
    int WindowHours,
    DateTimeOffset WindowStartedAt,
    int DelegationRequestedCount,
    int DelegationCompletedCount,
    int DelegationFailedCount,
    int DelegationFallbackCount,
    int DelegationBypassCount,
    int ActionableEventCount,
    int ProjectionNoiseCount,
    int LegacyDebtCount,
    IReadOnlyList<DelegationActorCount> Actors,
    IReadOnlyList<DelegationOutcomeCount> Outcomes,
    IReadOnlyList<DelegationEventPreview> RecentEvents);

public sealed record DelegationActorCount(string Actor, int Count);

public sealed record DelegationOutcomeCount(string Outcome, int Count);

public sealed record DelegationEventPreview(
    string EventKind,
    string Outcome,
    string Actor,
    string? TaskId,
    string? RunId,
    string Summary,
    DateTimeOffset OccurredAt,
    RuntimeNoiseAuditClassification Classification);

public sealed record ApprovalReport(
    DateTimeOffset GeneratedAt,
    int WindowHours,
    DateTimeOffset WindowStartedAt,
    int PendingRequestCount,
    IReadOnlyList<ApprovalDecisionCount> Decisions,
    IReadOnlyList<ApprovalActorCount> Actors,
    IReadOnlyList<ApprovalEventPreview> RecentEvents);

public sealed record ApprovalDecisionCount(string Decision, int Count);

public sealed record ApprovalActorCount(string Actor, int Count);

public sealed record ApprovalEventPreview(
    string PermissionRequestId,
    string TaskId,
    string Decision,
    string Actor,
    string ProviderId,
    string ReasonCode,
    DateTimeOffset OccurredAt);
