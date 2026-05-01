using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class OperationalSummaryService
{
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly DevLoopService devLoopService;
    private readonly WorkerPermissionOrchestrationService workerPermissionOrchestrationService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly ProviderHealthMonitorService providerHealthMonitorService;
    private readonly WorkerSelectionPolicyService workerSelectionPolicyService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;
    private readonly RuntimeNoiseAuditService runtimeNoiseAuditService;
    private readonly OperatorActionabilityService operatorActionabilityService;
    private readonly ProviderHealthActionabilityProjectionService providerHealthActionabilityProjectionService;

    public OperationalSummaryService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        DevLoopService devLoopService,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ProviderHealthMonitorService providerHealthMonitorService,
        WorkerSelectionPolicyService workerSelectionPolicyService,
        WorkerOperationalPolicyService operationalPolicyService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        RuntimeNoiseAuditService runtimeNoiseAuditService,
        OperatorActionabilityService operatorActionabilityService,
        ProviderHealthActionabilityProjectionService providerHealthActionabilityProjectionService)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.devLoopService = devLoopService;
        this.workerPermissionOrchestrationService = workerPermissionOrchestrationService;
        this.incidentTimelineService = incidentTimelineService;
        this.providerHealthMonitorService = providerHealthMonitorService;
        this.workerSelectionPolicyService = workerSelectionPolicyService;
        this.operationalPolicyService = operationalPolicyService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.runtimeNoiseAuditService = runtimeNoiseAuditService;
        this.operatorActionabilityService = operatorActionabilityService;
        this.providerHealthActionabilityProjectionService = providerHealthActionabilityProjectionService;
    }

    public OperationalSummary Build(bool refreshProviderHealth = true)
    {
        var policy = operationalPolicyService.GetPolicy().Observability;
        var projectionHealth = new MarkdownProjectionHealthService(paths).Load();
        var graph = taskGraphService.Load();
        var session = devLoopService.GetSession();
        var runtimeNoise = runtimeNoiseAuditService.Build();
        var actionableBlockedIds = runtimeNoise.BlockedTasks
            .Where(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
            .Select(item => item.TaskId)
            .ToHashSet(StringComparer.Ordinal);
        var actionableIncidentIds = runtimeNoise.Incidents
            .Where(item => item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker)
            .Select(item => item.IncidentId)
            .ToHashSet(StringComparer.Ordinal);
        var pendingApprovalRecords = workerPermissionOrchestrationService.ListPendingRequests();
        var pendingApprovals = pendingApprovalRecords
            .Take(Math.Max(1, policy.ApprovalQueuePreviewLimit))
            .Select(request => new OperationalQueueItem
            {
                ItemId = request.PermissionRequestId,
                TaskId = request.TaskId,
                Title = request.Summary,
                Category = request.Kind.ToString(),
                Reason = $"{request.ScopeSummary}; risk={request.RiskLevel}; recommended={request.RecommendedDecision}",
                Status = "pending",
                RecommendedNextAction = request.RecommendedDecision.ToString(),
            })
            .ToArray();
        var blockedTasks = graph.ListTasks()
            .Where(task => actionableBlockedIds.Contains(task.TaskId))
            .Select(task => BuildBlockedPreview(task, graph.CompletedTaskIds()))
            .Where(preview => preview is not null)
            .Select(preview => preview!)
            .ToArray();
        var providerHealthSnapshot = refreshProviderHealth ? providerHealthMonitorService.Refresh() : providerHealthMonitorService.Load();
        var selection = workerSelectionPolicyService.Evaluate();
        var preferredBackendId = operationalPolicyService.ResolvePreferredBackendId(repoId: null, fallbackBackendId: selection.SelectedBackendId);
        var providerProjection = providerHealthActionabilityProjectionService.Build(
            providerHealthSnapshot.Entries,
            selection,
            preferredBackendId);
        var providerHealth = providerProjection.Providers
            .Take(Math.Max(1, policy.IncidentPreviewLimit))
            .ToArray();
        var incidents = incidentTimelineService.Load()
            .Where(incident => actionableIncidentIds.Contains(incident.IncidentId))
            .Take(Math.Max(1, policy.IncidentPreviewLimit))
            .Select(incident => new OperationalIncidentDrilldown
            {
                IncidentId = incident.IncidentId,
                IncidentType = incident.IncidentType.ToString(),
                TaskId = incident.TaskId,
                RunId = incident.RunId,
                BackendId = incident.BackendId,
                Summary = incident.Summary,
                Consequence = incident.ConsequenceSummary,
                RecoveryAction = incident.RecoveryAction.ToString(),
                OccurredAt = incident.OccurredAt,
            })
            .ToArray();
        var recoveryOutcomes = incidentTimelineService.Load()
            .Where(item => item.RecoveryAction != Domain.Execution.WorkerRecoveryAction.None)
            .Take(Math.Max(1, policy.IncidentPreviewLimit))
            .Select(item => new OperationalRecoveryOutcome
            {
                TaskId = item.TaskId ?? "(none)",
                RecoveryAction = item.RecoveryAction.ToString(),
                Outcome = item.FailureKind == Domain.Execution.WorkerFailureKind.None ? "recovered" : item.FailureKind.ToString(),
                Summary = item.Summary,
                OccurredAt = item.OccurredAt,
            })
            .ToArray();
        var recentDelegation = operatorOsEventStreamService.Load()
            .Where(item => item.EventKind is OperatorOsEventKind.DelegationRequested
                or OperatorOsEventKind.DelegationCompleted
                or OperatorOsEventKind.DelegationFallbackUsed)
            .OrderByDescending(item => item.OccurredAt)
            .FirstOrDefault();
        var actionability = operatorActionabilityService.Assess(
            session,
            pendingApprovalRecords.Count,
            graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
            blockedTasks.Length,
            incidents.Length,
            runtimeNoise.ClassificationCounts.GetValueOrDefault("projection_noise") + runtimeNoise.ClassificationCounts.GetValueOrDefault("legacy_debt"),
            providerProjection);

        return new OperationalSummary
        {
            RepoId = "local-repo",
            Stage = RuntimeStageInfo.CurrentStage,
            SessionStatus = session?.Status.ToString() ?? "none",
            Actionability = OperatorActionabilityService.DescribeState(actionability.State),
            SessionActionability = RuntimeActionabilitySemantics.Describe(actionability.SessionActionability),
            ActionabilityReason = OperatorActionabilityService.DescribeReason(actionability.Reason),
            ActionabilitySummary = actionability.Summary,
            WorkerCount = session?.ActiveWorkerCount ?? 0,
            ActiveWorkerCount = session?.ActiveWorkerCount ?? 0,
            RunningTaskCount = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Running),
            PendingApprovalCount = pendingApprovalRecords.Count,
            BlockedTaskCount = blockedTasks.Length,
            ReviewTaskCount = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
            ActiveIncidentCount = incidents.Length,
            RecentIncidentCount = incidentTimelineService.Load().Count,
            ProjectionNoiseCount = runtimeNoise.ClassificationCounts.GetValueOrDefault("projection_noise"),
            LegacyDebtCount = runtimeNoise.ClassificationCounts.GetValueOrDefault("legacy_debt"),
            ProviderHealthIssueCount = providerProjection.Providers.Count(item => item.ActionabilityRelevant),
            OptionalProviderHealthIssueCount = providerProjection.OptionalIssueCount,
            DisabledProviderCount = providerProjection.DisabledIssueCount,
            PendingRebuildCount = graph.Tasks.Values.Count(task => task.LastRecoveryAction == Domain.Execution.WorkerRecoveryAction.RebuildWorktree),
            ProjectionWritebackState = projectionHealth.State,
            ProjectionWritebackSummary = projectionHealth.Summary,
            ProjectionWritebackFailureCount = projectionHealth.ConsecutiveFailureCount,
            LastDelegationTaskId = recentDelegation?.TaskId,
            RecommendedNextAction = actionability.RecommendedNextAction,
            ApprovalQueue = pendingApprovals,
            BlockedQueue = blockedTasks.Take(Math.Max(1, policy.BlockedQueuePreviewLimit)).ToArray(),
            Incidents = incidents,
            RecoveryOutcomes = recoveryOutcomes,
            Providers = providerHealth,
            Notes =
            [
                "Operational summary is a projection over current task, approval, provider, and incident truth.",
                "Preview queues are intentionally truncated for operator readability.",
                $"Projection writeback: state={projectionHealth.State}; consecutive_failures={projectionHealth.ConsecutiveFailureCount}; summary={projectionHealth.Summary}",
                $"Noise classifications: active_blocker={runtimeNoise.ClassificationCounts.GetValueOrDefault("active_blocker")}, projection_noise={runtimeNoise.ClassificationCounts.GetValueOrDefault("projection_noise")}, legacy_debt={runtimeNoise.ClassificationCounts.GetValueOrDefault("legacy_debt")}.",
                $"Actionability contract: state={OperatorActionabilityService.DescribeState(actionability.State)}, reason={OperatorActionabilityService.DescribeReason(actionability.Reason)}, session={RuntimeActionabilitySemantics.Describe(actionability.SessionActionability)}, actionable providers healthy/degraded/unavailable={actionability.HealthyProviderCount}/{actionability.DegradedProviderCount}/{actionability.UnavailableProviderCount}, optional={actionability.OptionalProviderIssueCount}, disabled={actionability.DisabledProviderCount}.",
            ],
        };
    }

    private static OperationalQueueItem? BuildBlockedPreview(TaskNode task, IReadOnlySet<string> completedTaskIds)
    {
        if (task.Status != DomainTaskStatus.Blocked
            && task.Status != DomainTaskStatus.ApprovalWait
            && task.Status != DomainTaskStatus.Review)
        {
            return null;
        }

        var unresolvedDependencies = task.Dependencies.Where(dependency => !completedTaskIds.Contains(dependency)).ToArray();
        var reason = ResolveBlockedReason(task, unresolvedDependencies);
        var nextAction = ResolveNextAction(task, unresolvedDependencies, reason);
        return new OperationalQueueItem
        {
            ItemId = task.TaskId,
            TaskId = task.TaskId,
            Title = task.Title,
            Category = reason,
            Reason = task.LastRecoveryReason ?? reason,
            Status = task.Status.ToString(),
            RecommendedNextAction = nextAction,
        };
    }

    private static string ResolveBlockedReason(TaskNode task, IReadOnlyList<string> unresolvedDependencies)
    {
        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return "approval";
        }

        if (task.Status == DomainTaskStatus.Review)
        {
            return "human_review";
        }

        if (unresolvedDependencies.Count > 0)
        {
            return "dependency";
        }

        return task.LastWorkerFailureKind switch
        {
            Domain.Execution.WorkerFailureKind.EnvironmentBlocked => "environment",
            Domain.Execution.WorkerFailureKind.PolicyDenied => "policy",
            Domain.Execution.WorkerFailureKind.ApprovalRequired => "approval",
            _ => "other",
        };
    }

    private static string ResolveNextAction(TaskNode task, IReadOnlyList<string> unresolvedDependencies, string reason)
    {
        return reason switch
        {
            "approval" => "resolve the pending permission decision",
            "human_review" => "review the task outcome and decide whether to approve or reject it",
            "dependency" => $"complete or unblock dependencies: {string.Join(", ", unresolvedDependencies)}",
            "environment" => task.LastRecoveryReason ?? "repair the runtime environment or rebuild the worktree",
            "policy" => task.LastRecoveryReason ?? "inspect worker policy and approval requirements",
            _ => task.LastRecoveryReason ?? "inspect task details and decide the next operator action",
        };
    }
}
