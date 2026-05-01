using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class GovernanceReportingService
{
    private readonly RepoRegistryService repoRegistryService;
    private readonly IRepoRuntimeGateway runtimeGateway;
    private readonly WorkerPermissionOrchestrationService workerPermissionOrchestrationService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly ProviderHealthMonitorService providerHealthMonitorService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;

    public GovernanceReportingService(
        RepoRegistryService repoRegistryService,
        IRepoRuntimeGateway runtimeGateway,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ProviderHealthMonitorService providerHealthMonitorService,
        WorkerOperationalPolicyService operationalPolicyService)
    {
        this.repoRegistryService = repoRegistryService;
        this.runtimeGateway = runtimeGateway;
        this.workerPermissionOrchestrationService = workerPermissionOrchestrationService;
        this.incidentTimelineService = incidentTimelineService;
        this.providerHealthMonitorService = providerHealthMonitorService;
        this.operationalPolicyService = operationalPolicyService;
    }

    public GovernanceReport Build(int? hours = null)
    {
        var policy = operationalPolicyService.GetPolicy().Observability;
        var windowHours = Math.Max(1, hours ?? policy.GovernanceReportDefaultHours);
        var windowStartedAt = DateTimeOffset.UtcNow.AddHours(-windowHours);
        var audit = workerPermissionOrchestrationService.LoadAudit()
            .Where(record => record.OccurredAt >= windowStartedAt)
            .ToArray();
        var incidents = incidentTimelineService.Load()
            .Where(record => record.OccurredAt >= windowStartedAt)
            .ToArray();
        var health = providerHealthMonitorService.Load().Entries;
        var taskLookup = BuildTaskLookup();

        var approvalEvents = audit
            .Where(record =>
                record.Decision is not null
                || record.EventKind == WorkerPermissionAuditEventKind.TimedOut)
            .OrderByDescending(record => record.OccurredAt)
            .ToArray();
        var approvalDecisions = approvalEvents
            .GroupBy(
                record => record.Decision?.ToString() ?? record.EventKind.ToString(),
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GovernanceDecisionCount(group.Key, group.Count()))
            .ToArray();
        var recentApprovals = approvalEvents
            .Take(10)
            .Select(record => new GovernanceApprovalPreview(
                record.PermissionRequestId,
                record.RepoId,
                record.TaskId,
                record.ProviderId,
                record.Decision?.ToString() ?? record.EventKind.ToString(),
                $"{record.ActorKind}:{record.ActorIdentity}",
                record.ReasonCode,
                record.OccurredAt))
            .ToArray();
        var unstableProviders = health
            .Select(record => new GovernanceProviderStabilitySummary(
                record.BackendId,
                record.ProviderId,
                record.State.ToString(),
                incidents.Count(incident => string.Equals(incident.BackendId, record.BackendId, StringComparison.Ordinal)),
                record.ConsecutiveFailureCount,
                record.LatencyMs,
                record.Summary))
            .Where(summary =>
                !string.Equals(summary.State, WorkerBackendHealthState.Healthy.ToString(), StringComparison.OrdinalIgnoreCase)
                || summary.IncidentCount > 0
                || summary.ConsecutiveFailureCount > 0)
            .OrderByDescending(summary => summary.IncidentCount)
            .ThenByDescending(summary => summary.ConsecutiveFailureCount)
            .ThenBy(summary => summary.BackendId, StringComparer.Ordinal)
            .ToArray();
        var permissionBlockedTaskClasses = approvalEvents
            .Where(record =>
                record.Decision is WorkerPermissionDecision.Deny or WorkerPermissionDecision.Review
                || record.EventKind == WorkerPermissionAuditEventKind.TimedOut)
            .GroupBy(record =>
            {
                if (taskLookup.TryGetValue((record.RepoId, record.TaskId), out var task))
                {
                    return task.TaskType.ToString();
                }

                return "Unknown";
            }, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GovernanceTaskClassCount(group.Key, group.Count()))
            .ToArray();
        var recoveryIncidents = incidents.Where(item => item.RecoveryAction != WorkerRecoveryAction.None).ToArray();
        var recoveredSuccessCount = recoveryIncidents.Count(incident =>
        {
            if (incident.TaskId is null || !taskLookup.TryGetValue((incident.RepoId, incident.TaskId), out var task))
            {
                return false;
            }

            return task.Status is not DomainTaskStatus.Blocked and not DomainTaskStatus.Failed;
        });
        var repoIncidentDensity = incidents
            .GroupBy(item => item.RepoId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GovernanceRepoIncidentDensity(group.Key, group.Count()))
            .ToArray();
        var incompleteTaskLookups = recoveryIncidents.Count(incident => incident.TaskId is null || !taskLookup.ContainsKey((incident.RepoId, incident.TaskId)));

        return new GovernanceReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            WindowHours: windowHours,
            WindowStartedAt: windowStartedAt,
            ApprovalDecisions: approvalDecisions,
            RecentApprovalEvents: recentApprovals,
            UnstableProviders: unstableProviders,
            PermissionBlockedTaskClasses: permissionBlockedTaskClasses,
            RecoverySampleCount: recoveryIncidents.Length,
            RecoverySuccessfulCount: recoveredSuccessCount,
            RecoverySuccessRate: recoveryIncidents.Length == 0 ? 1.0 : (double)recoveredSuccessCount / recoveryIncidents.Length,
            RepoIncidentDensity: repoIncidentDensity,
            IncompletenessNote: incompleteTaskLookups == 0
                ? "Governance report is derived from current authoritative truth within the requested time window."
                : $"Governance report is derived from current authoritative truth within the requested time window; {incompleteTaskLookups} recovery sample(s) could not be matched to a current task and are excluded from success-rate scoring.");
    }

    private Dictionary<(string RepoId, string TaskId), TaskNode> BuildTaskLookup()
    {
        var lookup = new Dictionary<(string RepoId, string TaskId), TaskNode>();
        foreach (var descriptor in repoRegistryService.List())
        {
            var graph = runtimeGateway.LoadTaskGraph(descriptor);
            foreach (var task in graph.Tasks.Values)
            {
                lookup[(descriptor.RepoId, task.TaskId)] = task;
            }
        }

        return lookup;
    }
}
