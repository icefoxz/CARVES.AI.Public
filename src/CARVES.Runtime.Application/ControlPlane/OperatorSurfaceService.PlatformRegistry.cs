using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RepoList()
    {
        return OperatorSurfaceFormatter.RepoList(repoRegistryService.List());
    }

    public OperatorCommandResult Hosts()
    {
        return OperatorSurfaceFormatter.Hosts(hostRegistryService.List());
    }

    public OperatorCommandResult RepoRegister(string repoPath, string? repoId, string? providerProfile, string? policyProfile)
    {
        var descriptor = repoRegistryService.Register(repoPath, repoId, providerProfile, policyProfile);
        platformGovernanceService.RecordEvent(GovernanceEventType.RepoRegistered, descriptor.RepoId, $"Registered repo {descriptor.RepoId}.");
        return OperatorSurfaceFormatter.RepoRegistered(descriptor);
    }

    public OperatorCommandResult RepoInspect(string repoId)
    {
        return OperatorSurfaceFormatter.RepoInspect(repoRegistryService.Inspect(repoId));
    }

    public OperatorCommandResult RuntimeList()
    {
        return OperatorSurfaceFormatter.RuntimeInstances(runtimeInstanceManager.List());
    }

    public OperatorCommandResult RuntimeInspect(string repoId)
    {
        return OperatorSurfaceFormatter.RuntimeInstanceInspect(runtimeInstanceManager.Inspect(repoId));
    }

    public OperatorCommandResult RuntimeStart(string repoId, bool dryRun)
    {
        return OperatorSurfaceFormatter.RuntimeInstanceChanged("Started", runtimeInstanceManager.Start(repoId, dryRun));
    }

    public OperatorCommandResult RuntimeResume(string repoId, string reason)
    {
        return OperatorSurfaceFormatter.RuntimeInstanceChanged("Resumed", runtimeInstanceManager.Resume(repoId, reason));
    }

    public OperatorCommandResult RuntimePause(string repoId, string reason)
    {
        return OperatorSurfaceFormatter.RuntimeInstanceChanged("Paused", runtimeInstanceManager.Pause(repoId, reason));
    }

    public OperatorCommandResult RuntimeStop(string repoId, string reason)
    {
        return OperatorSurfaceFormatter.RuntimeInstanceChanged("Stopped", runtimeInstanceManager.Stop(repoId, reason));
    }

    public OperatorCommandResult RuntimeSchedule(int requestedSlots)
    {
        return OperatorSurfaceFormatter.PlatformSchedule(platformSchedulerService.Plan(requestedSlots));
    }

    public OperatorCommandResult ProviderList()
    {
        return OperatorSurfaceFormatter.ProviderList(providerRegistryService.List());
    }

    public OperatorCommandResult ProviderInspect(string providerId)
    {
        return OperatorSurfaceFormatter.ProviderInspect(providerRegistryService.Inspect(providerId));
    }

    public OperatorCommandResult ProviderBind(string repoId, string profileId)
    {
        return OperatorSurfaceFormatter.ProviderBound(providerRegistryService.Bind(repoId, profileId));
    }

    public OperatorCommandResult ProviderQuota()
    {
        return OperatorSurfaceFormatter.ProviderQuota(operatorApiService.GetProviderQuotas());
    }

    public OperatorCommandResult ProviderRoute(string repoId, string role, bool allowFallback)
    {
        var decision = providerRoutingService.Route(repoId, role, allowFallback);
        if (!decision.Allowed)
        {
            platformGovernanceService.RecordEvent(GovernanceEventType.ProviderQuotaDenied, repoId, decision.Reason);
        }
        else if (decision.UsedFallback)
        {
            platformGovernanceService.RecordEvent(GovernanceEventType.ProviderFallbackUsed, repoId, decision.Reason);
        }

        return OperatorSurfaceFormatter.ProviderRoute(decision);
    }
}
