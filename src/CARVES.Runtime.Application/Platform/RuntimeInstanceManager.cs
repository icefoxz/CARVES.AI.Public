using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeInstanceManager
{
    private readonly IRuntimeInstanceRepository repository;
    private readonly RepoRegistryService repoRegistryService;
    private readonly RepoRuntimeService repoRuntimeService;
    private readonly IRepoRuntimeGateway runtimeGateway;
    private readonly PlatformGovernanceService governanceService;
    private readonly RepoTruthProjectionService projectionService;

    public RuntimeInstanceManager(
        IRuntimeInstanceRepository repository,
        RepoRegistryService repoRegistryService,
        RepoRuntimeService repoRuntimeService,
        IRepoRuntimeGateway runtimeGateway,
        PlatformGovernanceService governanceService,
        RepoTruthProjectionService projectionService)
    {
        this.repository = repository;
        this.repoRegistryService = repoRegistryService;
        this.repoRuntimeService = repoRuntimeService;
        this.runtimeGateway = runtimeGateway;
        this.governanceService = governanceService;
        this.projectionService = projectionService;
    }

    public IReadOnlyList<RuntimeInstance> List()
    {
        var descriptors = repoRegistryService.List();
        PruneOrphanedInstances(descriptors);
        return descriptors
            .Select(EnsureInstance)
            .Select(instance => RefreshProjection(instance.RepoId))
            .OrderBy(instance => instance.RepoId, StringComparer.Ordinal)
            .ToArray();
    }

    public RuntimeInstance Inspect(string repoId)
    {
        EnsureInstance(repoRegistryService.Inspect(repoId));
        return RefreshProjection(repoId);
    }

    public RuntimeInstance Start(string repoId, bool dryRun)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var session = runtimeGateway.Start(descriptor, dryRun);
        var instance = EnsureInstance(descriptor);
        instance.Status = RuntimeInstanceStatus.Running;
        instance.ActiveSessionId = session.SessionId;
        instance.Stage = RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage;
        instance.ProviderBindingId = descriptor.ProviderProfile;
        instance.PolicyBindingId = descriptor.PolicyProfile;
        instance.GatewayMode = runtimeGateway.GatewayMode;
        Save(instance);
        repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(instance.Status));
        governanceService.RecordEvent(GovernanceEventType.RuntimeStarted, repoId, $"Runtime instance started for {repoId}.");
        return RefreshProjection(repoId);
    }

    public RuntimeInstance Resume(string repoId, string reason)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var session = runtimeGateway.Resume(descriptor, reason);
        var instance = EnsureInstance(descriptor);
        instance.Status = RuntimeInstanceStatus.Running;
        instance.ActiveSessionId = session.SessionId;
        instance.Stage = RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage;
        Save(instance);
        repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(instance.Status));
        governanceService.RecordEvent(GovernanceEventType.RuntimeStarted, repoId, $"Runtime instance resumed for {repoId}. {reason}");
        return RefreshProjection(repoId);
    }

    public RuntimeInstance Pause(string repoId, string reason)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var session = runtimeGateway.Pause(descriptor, reason);
        var instance = EnsureInstance(descriptor);
        instance.Status = RuntimeInstanceStatus.Paused;
        instance.ActiveSessionId = session.SessionId;
        instance.Stage = RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage;
        Save(instance);
        repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(instance.Status));
        governanceService.RecordEvent(GovernanceEventType.RuntimePaused, repoId, reason);
        return RefreshProjection(repoId);
    }

    public RuntimeInstance Stop(string repoId, string reason)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var session = runtimeGateway.Stop(descriptor, reason);
        var instance = EnsureInstance(descriptor);
        instance.Status = RuntimeInstanceStatus.Stopped;
        instance.ActiveSessionId = session.SessionId;
        instance.Stage = RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage;
        Save(instance);
        repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(instance.Status));
        governanceService.RecordEvent(GovernanceEventType.RuntimeStopped, repoId, reason);
        return RefreshProjection(repoId);
    }

    public RepoRuntimeSummary LoadRuntimeSummary(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        return runtimeGateway.LoadSummary(descriptor);
    }

    private RuntimeInstance EnsureInstance(RepoDescriptor descriptor)
    {
        var stage = RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage;
        var existing = repository.Load().FirstOrDefault(instance => string.Equals(instance.RepoId, descriptor.RepoId, StringComparison.Ordinal));
        if (existing is not null)
        {
            return ReconcileExistingInstance(descriptor, stage, existing);
        }

        var instance = new RuntimeInstance
        {
            RepoId = descriptor.RepoId,
            RepoPath = descriptor.RepoPath,
            Stage = stage,
            Status = RuntimeInstanceStatus.Registered,
            ProviderBindingId = descriptor.ProviderProfile,
            PolicyBindingId = descriptor.PolicyProfile,
            GatewayMode = runtimeGateway.GatewayMode,
        };
        Save(instance);
        repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(instance.Status));
        return instance;
    }

    private RuntimeInstance ReconcileExistingInstance(RepoDescriptor descriptor, string stage, RuntimeInstance existing)
    {
        var repoPathChanged = !string.Equals(existing.RepoPath, descriptor.RepoPath, StringComparison.OrdinalIgnoreCase);
        var requiresReconciliation =
            repoPathChanged
            || !string.Equals(existing.Stage, stage, StringComparison.Ordinal)
            || !string.Equals(existing.ProviderBindingId, descriptor.ProviderProfile, StringComparison.Ordinal)
            || !string.Equals(existing.PolicyBindingId, descriptor.PolicyProfile, StringComparison.Ordinal)
            || existing.GatewayMode != runtimeGateway.GatewayMode;
        if (!requiresReconciliation)
        {
            return existing;
        }

        var reconciled = new RuntimeInstance
        {
            SchemaVersion = existing.SchemaVersion,
            RepoId = existing.RepoId,
            RepoPath = descriptor.RepoPath,
            Stage = stage,
            Status = existing.Status,
            ActiveSessionId = existing.ActiveSessionId,
            ProviderBindingId = descriptor.ProviderProfile,
            PolicyBindingId = descriptor.PolicyProfile,
            Projection = existing.Projection,
            GatewayMode = runtimeGateway.GatewayMode,
            GatewayHealth = existing.GatewayHealth,
            GatewayReason = existing.GatewayReason,
            LastPlatformScheduledAt = existing.LastPlatformScheduledAt,
            PlatformSelectionCount = existing.PlatformSelectionCount,
            LastFairnessScore = existing.LastFairnessScore,
            LastSchedulingReason = existing.LastSchedulingReason,
            UpdatedAt = existing.UpdatedAt,
        };
        Save(reconciled);
        if (repoPathChanged)
        {
            repoRuntimeService.Upsert(descriptor.RepoPath, RepoRuntimeService.FromRuntimeInstanceStatus(reconciled.Status));
        }

        return reconciled;
    }

    private void PruneOrphanedInstances(IReadOnlyList<RepoDescriptor> descriptors)
    {
        var allowedRepoIds = descriptors
            .Select(descriptor => descriptor.RepoId)
            .ToHashSet(StringComparer.Ordinal);
        var persisted = repository.Load();
        var pruned = persisted
            .Where(instance => allowedRepoIds.Contains(instance.RepoId))
            .OrderBy(instance => instance.RepoId, StringComparer.Ordinal)
            .ToArray();
        if (pruned.Length == persisted.Count)
        {
            return;
        }

        repository.Save(pruned);
    }

    public RuntimeInstance Update(RuntimeInstance instance)
    {
        Save(instance);
        return instance;
    }

    private RuntimeInstance RefreshProjection(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var instance = repository.Load().First(existing => string.Equals(existing.RepoId, repoId, StringComparison.Ordinal));
        var refreshed = projectionService.Refresh(descriptor, instance);
        Save(refreshed);
        return refreshed;
    }

    private void Save(RuntimeInstance instance)
    {
        instance.Touch();
        var instances = repository.Load().Where(existing => !string.Equals(existing.RepoId, instance.RepoId, StringComparison.Ordinal)).ToList();
        instances.Add(instance);
        repository.Save(instances.OrderBy(existing => existing.RepoId, StringComparer.Ordinal).ToArray());
    }
}
