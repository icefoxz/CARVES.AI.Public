using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class OperatorApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly RepoRegistryService repoRegistryService;
    private readonly RuntimeInstanceManager runtimeInstanceManager;
    private readonly ProviderRegistryService providerRegistryService;
    private readonly ProviderRoutingService providerRoutingService;
    private readonly PlatformSchedulerService platformSchedulerService;
    private readonly IWorkerNodeRegistryRepository workerNodeRegistryRepository;
    private readonly IWorkerLeaseRepository workerLeaseRepository;
    private readonly IRepoRuntimeGateway runtimeGateway;
    private readonly WorkerExecutionBoundaryService workerExecutionBoundaryService;
    private readonly WorkerSelectionPolicyService workerSelectionPolicyService;
    private readonly WorkerPermissionOrchestrationService workerPermissionOrchestrationService;
    private readonly ProviderHealthMonitorService providerHealthMonitorService;
    private readonly RuntimeRoutingProfileService runtimeRoutingProfileService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly ActorSessionService actorSessionService;
    private readonly SessionOwnershipService sessionOwnershipService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;

    public OperatorApiService(
        RepoRegistryService repoRegistryService,
        RuntimeInstanceManager runtimeInstanceManager,
        ProviderRegistryService providerRegistryService,
        ProviderRoutingService providerRoutingService,
        PlatformSchedulerService platformSchedulerService,
        IWorkerNodeRegistryRepository workerNodeRegistryRepository,
        IWorkerLeaseRepository workerLeaseRepository,
        IRepoRuntimeGateway runtimeGateway,
        WorkerExecutionBoundaryService workerExecutionBoundaryService,
        WorkerSelectionPolicyService workerSelectionPolicyService,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        ProviderHealthMonitorService providerHealthMonitorService,
        RuntimeRoutingProfileService runtimeRoutingProfileService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        SessionOwnershipService sessionOwnershipService,
        OperatorOsEventStreamService operatorOsEventStreamService)
    {
        this.repoRegistryService = repoRegistryService;
        this.runtimeInstanceManager = runtimeInstanceManager;
        this.providerRegistryService = providerRegistryService;
        this.providerRoutingService = providerRoutingService;
        this.platformSchedulerService = platformSchedulerService;
        this.workerNodeRegistryRepository = workerNodeRegistryRepository;
        this.workerLeaseRepository = workerLeaseRepository;
        this.runtimeGateway = runtimeGateway;
        this.workerExecutionBoundaryService = workerExecutionBoundaryService;
        this.workerSelectionPolicyService = workerSelectionPolicyService;
        this.workerPermissionOrchestrationService = workerPermissionOrchestrationService;
        this.providerHealthMonitorService = providerHealthMonitorService;
        this.runtimeRoutingProfileService = runtimeRoutingProfileService;
        this.incidentTimelineService = incidentTimelineService;
        this.actorSessionService = actorSessionService;
        this.sessionOwnershipService = sessionOwnershipService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
    }

    public PlatformStatusSummary GetPlatformStatus()
    {
        var repos = repoRegistryService.List();
        var instances = runtimeInstanceManager.List();
        var providers = providerRegistryService.List();
        var nodes = workerNodeRegistryRepository.Load();
        var leases = workerLeaseRepository.Load();
        var repoSummaries = repos.Select(descriptor =>
        {
            var instance = instances.First(item => string.Equals(item.RepoId, descriptor.RepoId, StringComparison.Ordinal));
            return new RepoRuntimeSummaryDto(
                descriptor.RepoId,
                descriptor.RepoPath,
                instance.Projection.Stage,
                instance.Projection.SessionStatus?.ToString() ?? instance.Status.ToString(),
                instance.Projection.OpenTaskCount,
                instance.Projection.ReviewTaskCount,
                instance.Projection.OpenOpportunityCount,
                RuntimeActionabilitySemantics.Describe(instance.Projection.Actionability),
                descriptor.ProviderProfile,
                descriptor.PolicyProfile,
                instance.Projection.TruthSource.ToString(),
                instance.Projection.Freshness.ToString(),
                instance.Projection.LastReconciliationOutcome.ToString(),
                instance.GatewayMode.ToString(),
                instance.GatewayHealth.ToString(),
                instance.GatewayReason,
                instance.LastSchedulingReason);
        }).ToArray();

        return new PlatformStatusSummary(
            repos.Count,
            instances.Count,
            instances.Count(instance => instance.Status == RuntimeInstanceStatus.Running),
            repoSummaries.Sum(summary => summary.OpenOpportunities),
            repoSummaries.Count(summary => !string.Equals(summary.RuntimeStatus, "none", StringComparison.OrdinalIgnoreCase)),
            providers.Count,
            nodes.Count,
            leases.Count(lease => lease.Status == WorkerLeaseStatus.Active),
            repoSummaries.Count(summary => !string.Equals(summary.ProjectionFreshness, ProjectionFreshness.Fresh.ToString(), StringComparison.Ordinal)),
            repoSummaries);
    }

    public IReadOnlyList<RepoRuntimeSummaryDto> GetRepos()
    {
        return GetPlatformStatus().Repos;
    }

    public RepoRuntimeSummaryDto GetRepoRuntime(string repoId)
    {
        return GetPlatformStatus().Repos.First(repo => string.Equals(repo.RepoId, repoId, StringComparison.Ordinal));
    }

    public IReadOnlyList<WorkerLeaseSummaryDto> GetWorkerLeases()
    {
        return workerLeaseRepository.Load()
            .OrderByDescending(lease => lease.AcquiredAt)
            .Select(lease => new WorkerLeaseSummaryDto(
                lease.LeaseId,
                lease.RepoPath,
                lease.TaskId,
                lease.NodeId,
                lease.Status.ToString(),
                lease.OnExpiry.ToString(),
                lease.ExpiresAt,
                lease.CompletionReason))
            .ToArray();
    }

    public IReadOnlyList<ProviderQuotaSummaryDto> GetProviderQuotas()
    {
        return providerRoutingService.GetQuotaSnapshot().Entries
            .OrderBy(entry => entry.ProfileId, StringComparer.Ordinal)
            .Select(entry => new ProviderQuotaSummaryDto(entry.ProfileId, entry.UsedThisHour, entry.LimitPerHour, entry.Remaining, entry.Exhausted))
            .ToArray();
    }

    public ProviderRoutingDecision GetProviderRoute(string repoId, string role, bool allowFallback)
    {
        return providerRoutingService.Route(repoId, role, allowFallback);
    }

    public PlatformSchedulingDecision GetPlatformSchedule(int requestedSlots)
    {
        return platformSchedulerService.Plan(requestedSlots);
    }

    public RepoRuntimeGatewayHealth GetRepoGateway(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        return runtimeGateway.GetHealth(descriptor);
    }

    public TaskGraphSummaryDto GetRepoTasks(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var graph = runtimeGateway.LoadTaskGraph(descriptor);
        return new TaskGraphSummaryDto(
            repoId,
            graph.Tasks.Count,
            graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Pending),
            graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
            graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Completed),
            graph.Tasks.Values
                .OrderBy(task => task.TaskId, StringComparer.Ordinal)
                .Select(task => new TaskSummaryDto(task.TaskId, task.Title, task.Status.ToString(), task.TaskType.ToString(), task.ProposalSource.ToString()))
                .ToArray());
    }

    public IReadOnlyList<OpportunitySummaryDto> GetRepoOpportunities(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        return runtimeGateway.LoadOpportunities(descriptor).Items
            .OrderBy(item => item.OpportunityId, StringComparer.Ordinal)
            .Select(item => new OpportunitySummaryDto(
                item.OpportunityId,
                item.Source.ToString(),
                item.Severity.ToString(),
                item.Confidence,
                item.Status.ToString(),
                item.RelatedFiles,
                item.MaterializedTaskIds))
            .ToArray();
    }

    public SessionSummaryDto GetRepoSession(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var session = runtimeGateway.LoadSession(descriptor);
        return new SessionSummaryDto(
            repoId,
            session?.Status,
            session is null ? RuntimeActionabilitySemantics.Describe(RuntimeActionability.None) : RuntimeActionabilitySemantics.Describe(session.CurrentActionability),
            session?.StopReason,
            session?.WaitingReason,
            session?.PlannerRound ?? 0,
            session?.ActiveWorkerCount ?? 0,
            session?.ActiveTaskIds ?? Array.Empty<string>());
    }

    public IReadOnlyList<WorkerBackendDescriptor> GetWorkerProviders()
    {
        return providerRegistryService.ListWorkerBackends();
    }

    public RuntimeRoutingProfile? GetActiveRoutingProfile()
    {
        return runtimeRoutingProfileService.LoadActive();
    }

    public IReadOnlyList<Carves.Runtime.Domain.Execution.WorkerExecutionProfile> GetWorkerProfiles(string? repoId)
    {
        return workerExecutionBoundaryService.ListProfiles(repoId);
    }

    public Carves.Runtime.Domain.Execution.WorkerSelectionDecision GetWorkerSelection(string repoId, string? taskId)
    {
        Carves.Runtime.Domain.Tasks.TaskNode? task = null;
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            var descriptor = repoRegistryService.Inspect(repoId);
            var graph = runtimeGateway.LoadTaskGraph(descriptor);
            task = graph.Tasks.TryGetValue(taskId, out var node) ? node : null;
        }

        return workerSelectionPolicyService.Evaluate(task, repoId);
    }

    public IReadOnlyList<Carves.Runtime.Domain.Execution.WorkerPermissionRequest> GetPendingWorkerPermissionRequests()
    {
        return workerPermissionOrchestrationService.ListPendingRequests();
    }

    public IReadOnlyList<Carves.Runtime.Domain.Execution.WorkerPermissionAuditRecord> GetWorkerPermissionAudit(string? taskId = null, string? permissionRequestId = null)
    {
        return workerPermissionOrchestrationService.LoadAudit(taskId, permissionRequestId);
    }

    public IReadOnlyList<ProviderHealthRecord> GetWorkerHealth(bool refresh, string? backendId = null)
    {
        var snapshot = refresh ? providerHealthMonitorService.Refresh() : providerHealthMonitorService.Load();
        return snapshot.Entries
            .Where(entry => string.IsNullOrWhiteSpace(backendId) || string.Equals(entry.BackendId, backendId, StringComparison.Ordinal))
            .OrderBy(entry => entry.BackendId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<RuntimeIncidentRecord> GetRuntimeIncidents(string? taskId = null, string? runId = null)
    {
        return incidentTimelineService.Load(taskId, runId);
    }

    public IReadOnlyList<ActorSessionRecord> GetActorSessions(ActorSessionKind? kind = null)
    {
        return actorSessionService.List(kind);
    }

    public IReadOnlyList<OwnershipBinding> GetOwnershipBindings(OwnershipScope? scope = null, string? targetId = null)
    {
        return sessionOwnershipService.List(scope, targetId);
    }

    public IReadOnlyList<OperatorOsEventRecord> GetOperatorOsEvents(string? taskId = null, string? actorSessionId = null, OperatorOsEventKind? eventKind = null)
    {
        return operatorOsEventStreamService.Load(taskId, actorSessionId, eventKind);
    }

    public string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
