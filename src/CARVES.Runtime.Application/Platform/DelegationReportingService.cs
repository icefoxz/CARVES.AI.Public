using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class DelegationReportingService
{
    private readonly RuntimeNoiseAuditService runtimeNoiseAuditService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;
    private readonly WorkerPermissionOrchestrationService workerPermissionOrchestrationService;
    private readonly WorkerOperationalPolicyService workerOperationalPolicyService;

    public DelegationReportingService(
        RuntimeNoiseAuditService runtimeNoiseAuditService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        WorkerOperationalPolicyService workerOperationalPolicyService)
    {
        this.runtimeNoiseAuditService = runtimeNoiseAuditService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.workerPermissionOrchestrationService = workerPermissionOrchestrationService;
        this.workerOperationalPolicyService = workerOperationalPolicyService;
    }

    public DelegationReport BuildDelegationReport(int? hours = null)
    {
        var policy = workerOperationalPolicyService.GetPolicy().Observability;
        var windowHours = Math.Max(1, hours ?? policy.GovernanceReportDefaultHours);
        var windowStartedAt = DateTimeOffset.UtcNow.AddHours(-windowHours);
        var events = operatorOsEventStreamService.Load()
            .Where(record =>
                record.OccurredAt >= windowStartedAt
                && record.EventKind is OperatorOsEventKind.DelegationRequested
                    or OperatorOsEventKind.DelegationCompleted
                    or OperatorOsEventKind.DelegationFallbackUsed
                    or OperatorOsEventKind.DelegationBypassDetected)
            .ToArray();
        var classifiedEvents = events
            .Select(record => new
            {
                Record = record,
                Classification = runtimeNoiseAuditService.ClassifyTaskReference(record.TaskId, record.OccurredAt),
            })
            .ToArray();
        var previewEvents = classifiedEvents
            .Where(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
            .OrderByDescending(item => item.Record.OccurredAt)
            .Take(20)
            .ToArray();

        return new DelegationReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            WindowHours: windowHours,
            WindowStartedAt: windowStartedAt,
            DelegationRequestedCount: events.Count(item => item.EventKind == OperatorOsEventKind.DelegationRequested),
            DelegationCompletedCount: events.Count(item =>
                item.EventKind == OperatorOsEventKind.DelegationCompleted
                && string.Equals(item.ReasonCode, "delegation_completed", StringComparison.Ordinal)),
            DelegationFailedCount: events.Count(item =>
                item.EventKind == OperatorOsEventKind.DelegationCompleted
                && (string.Equals(item.ReasonCode, "delegation_failed", StringComparison.Ordinal)
                    || string.Equals(item.ReasonCode, "delegation_rejected", StringComparison.Ordinal))),
            DelegationFallbackCount: events.Count(item => item.EventKind == OperatorOsEventKind.DelegationFallbackUsed),
            DelegationBypassCount: events.Count(item => item.EventKind == OperatorOsEventKind.DelegationBypassDetected),
            ActionableEventCount: classifiedEvents.Count(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker),
            ProjectionNoiseCount: classifiedEvents.Count(item => item.Classification == RuntimeNoiseAuditClassification.ProjectionNoise),
            LegacyDebtCount: classifiedEvents.Count(item => item.Classification == RuntimeNoiseAuditClassification.LegacyDebt),
            Actors: classifiedEvents
                .Where(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
                .GroupBy(item => $"{item.Record.ActorKind?.ToString() ?? "Unknown"}:{item.Record.ActorIdentity ?? "(unknown)"}", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DelegationActorCount(group.Key, group.Count()))
                .ToArray(),
            Outcomes: classifiedEvents
                .Where(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
                .GroupBy(item => ResolveDelegationOutcome(item.Record), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DelegationOutcomeCount(group.Key, group.Count()))
                .ToArray(),
            RecentEvents: previewEvents
                .Select(item => new DelegationEventPreview(
                    item.Record.EventKind.ToString(),
                    ResolveDelegationOutcome(item.Record),
                    $"{item.Record.ActorKind?.ToString() ?? "Unknown"}:{item.Record.ActorIdentity ?? "(unknown)"}",
                    item.Record.TaskId,
                    item.Record.RunId,
                    item.Record.Summary,
                    item.Record.OccurredAt,
                    item.Classification))
                .ToArray());
    }

    public ApprovalReport BuildApprovalReport(int? hours = null)
    {
        var policy = workerOperationalPolicyService.GetPolicy().Observability;
        var windowHours = Math.Max(1, hours ?? policy.GovernanceReportDefaultHours);
        var windowStartedAt = DateTimeOffset.UtcNow.AddHours(-windowHours);
        var audit = workerPermissionOrchestrationService.LoadAudit()
            .Where(record => record.OccurredAt >= windowStartedAt)
            .ToArray();
        var decisionEvents = audit
            .Where(record => record.Decision is not null || record.EventKind == WorkerPermissionAuditEventKind.TimedOut)
            .ToArray();

        return new ApprovalReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            WindowHours: windowHours,
            WindowStartedAt: windowStartedAt,
            PendingRequestCount: workerPermissionOrchestrationService.ListPendingRequests().Count,
            Decisions: decisionEvents
                .GroupBy(
                    record => record.Decision?.ToString() ?? record.EventKind.ToString(),
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ApprovalDecisionCount(group.Key, group.Count()))
                .ToArray(),
            Actors: decisionEvents
                .GroupBy(record => $"{record.ActorKind}:{record.ActorIdentity}", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ApprovalActorCount(group.Key, group.Count()))
                .ToArray(),
            RecentEvents: decisionEvents
                .OrderByDescending(record => record.OccurredAt)
                .Take(20)
                .Select(record => new ApprovalEventPreview(
                    record.PermissionRequestId,
                    record.TaskId,
                    record.Decision?.ToString() ?? record.EventKind.ToString(),
                    $"{record.ActorKind}:{record.ActorIdentity}",
                    record.ProviderId,
                    record.ReasonCode,
                    record.OccurredAt))
                .ToArray());
    }

    private static string ResolveDelegationOutcome(OperatorOsEventRecord record)
    {
        return record.EventKind switch
        {
            OperatorOsEventKind.DelegationRequested => "requested",
            OperatorOsEventKind.DelegationFallbackUsed => "manual_fallback",
            OperatorOsEventKind.DelegationBypassDetected => "bypass_detected",
            OperatorOsEventKind.DelegationCompleted when string.Equals(record.ReasonCode, "delegation_completed", StringComparison.Ordinal) => "completed",
            OperatorOsEventKind.DelegationCompleted when string.Equals(record.ReasonCode, "delegation_rejected", StringComparison.Ordinal) => "rejected",
            OperatorOsEventKind.DelegationCompleted when string.Equals(record.ReasonCode, "delegation_failed", StringComparison.Ordinal) => "failed",
            _ => record.ReasonCode,
        };
    }
}
