using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RepoList(IReadOnlyList<RepoDescriptor> descriptors)
    {
        var lines = new List<string> { "Repos:" };
        lines.AddRange(descriptors.Count == 0
            ? ["(none)"]
            : descriptors.Select(descriptor => $"- {descriptor.RepoId}: {descriptor.RepoPath} [stage={descriptor.Stage}; provider={descriptor.ProviderProfile}; policy={descriptor.PolicyProfile}]"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult Hosts(IReadOnlyList<HostInstance> hosts)
    {
        var lines = new List<string> { "Hosts:" };
        lines.AddRange(hosts.Count == 0
            ? ["(none)"]
            : hosts.Select(host => $"- {host.HostId} ({host.Status.ToString().ToLowerInvariant()}): machine={host.MachineId}; endpoint={host.Endpoint}; last_seen={host.LastSeen:O}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult FleetStatus(FleetSnapshot snapshot, bool full)
    {
        var lines = new List<string> { "Fleet Snapshot:", string.Empty, "Hosts:" };
        lines.AddRange(snapshot.Hosts.Count == 0
            ? ["(none)"]
            : snapshot.Hosts.Select(host => full
                ? $"- {host.HostId} ({host.Status.ToString().ToLowerInvariant()}): machine={host.MachineId}; endpoint={host.Endpoint}; last_seen={host.LastSeen:O}"
                : $"- {host.HostId} ({host.Status.ToString().ToLowerInvariant()})"));

        lines.Add(string.Empty);
        lines.Add("Repos:");
        lines.AddRange(snapshot.Repos.Count == 0
            ? ["(none)"]
            : snapshot.Repos.Select(repo => full
                ? $"- {repo.RepoId} ({repo.RepoStatus.ToString().ToLowerInvariant()}): path={repo.RepoPath}; host={FormatHostBinding(repo)}; mapping={FormatMappingState(repo.MappingState)}; host_status={FormatHostStatus(repo.HostStatus)}; last_seen={repo.LastSeen:O}"
                : $"- {repo.RepoId} ({repo.RepoStatus.ToString().ToLowerInvariant()}): host={FormatHostBinding(repo)}; mapping={FormatMappingState(repo.MappingState)}"));

        lines.Add(string.Empty);
        lines.Add("Mappings:");
        lines.AddRange(snapshot.Repos.Count == 0
            ? ["(none)"]
            : snapshot.Repos.Select(repo => full
                ? $"- {repo.RepoId} -> {FormatHostBinding(repo)} ({FormatMappingState(repo.MappingState)}; repo_status={repo.RepoStatus.ToString().ToLowerInvariant()}; host_status={FormatHostStatus(repo.HostStatus)})"
                : $"- {repo.RepoId} -> {FormatHostBinding(repo)} ({FormatMappingState(repo.MappingState)})"));
        return new OperatorCommandResult(0, lines);

        static string FormatHostBinding(FleetRepoSnapshot repo)
        {
            return repo.MappingState == RepoHostMappingState.Mapped && !string.IsNullOrWhiteSpace(repo.HostId)
                ? repo.HostId
                : "(orphan)";
        }

        static string FormatMappingState(RepoHostMappingState state)
        {
            return state switch
            {
                RepoHostMappingState.Mapped => "mapped",
                RepoHostMappingState.Orphaned => "orphan",
                RepoHostMappingState.UnknownRepo => "unknown-repo",
                _ => state.ToString().ToLowerInvariant(),
            };
        }

        static string FormatHostStatus(HostInstanceStatus? status)
        {
            return status?.ToString().ToLowerInvariant() ?? "(none)";
        }
    }

    public static OperatorCommandResult RepoRegistered(RepoDescriptor descriptor)
    {
        return OperatorCommandResult.Success(
            $"Registered repo {descriptor.RepoId}.",
            $"Path: {descriptor.RepoPath}",
            $"Stage: {descriptor.Stage}",
            $"Provider profile: {descriptor.ProviderProfile}",
            $"Policy profile: {descriptor.PolicyProfile}");
    }

    public static OperatorCommandResult RepoInspect(RepoDescriptor descriptor)
    {
        return OperatorCommandResult.Success(
            $"Repo ID: {descriptor.RepoId}",
            $"Path: {descriptor.RepoPath}",
            $"Stage: {descriptor.Stage}",
            $"Runtime enabled: {descriptor.RuntimeEnabled}",
            $"Provider profile: {descriptor.ProviderProfile}",
            $"Policy profile: {descriptor.PolicyProfile}");
    }

    public static OperatorCommandResult RuntimeInstances(IReadOnlyList<RuntimeInstance> instances)
    {
        var lines = new List<string> { "Runtime instances:" };
        lines.AddRange(instances.Count == 0
            ? ["(none)"]
            : instances.Select(instance => $"- {instance.RepoId}: {instance.Status} [session={instance.ActiveSessionId ?? "(none)"}; provider={instance.ProviderBindingId}; policy={instance.PolicyBindingId}; truth={instance.Projection.TruthSource}/{instance.Projection.Freshness}; gateway={instance.GatewayMode}/{instance.GatewayHealth}]"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult RuntimeInstanceInspect(RuntimeInstance instance)
    {
        return OperatorCommandResult.Success(
            $"Runtime instance: {instance.RepoId}",
            $"Path: {instance.RepoPath}",
            $"Status: {instance.Status}",
            $"Stage: {instance.Stage}",
            $"Active session: {instance.ActiveSessionId ?? "(none)"}",
            $"Provider binding: {instance.ProviderBindingId}",
            $"Policy binding: {instance.PolicyBindingId}",
            $"Projection truth: {instance.Projection.TruthSource}",
            $"Projection freshness: {instance.Projection.Freshness}",
            $"Projection outcome: {instance.Projection.LastReconciliationOutcome}",
            $"Projection reason: {instance.Projection.FreshnessReason ?? "(none)"}",
            $"Gateway: {instance.GatewayMode} / {instance.GatewayHealth}",
            $"Gateway reason: {instance.GatewayReason ?? "(none)"}",
            $"Last scheduling reason: {instance.LastSchedulingReason ?? "(none)"}",
            $"Fairness score: {instance.LastFairnessScore:0.00}");
    }

    public static OperatorCommandResult RuntimeInstanceChanged(string action, RuntimeInstance instance)
    {
        return OperatorCommandResult.Success(
            $"{action} runtime instance for {instance.RepoId}.",
            $"Status: {instance.Status}",
            $"Stage: {instance.Stage}",
            $"Active session: {instance.ActiveSessionId ?? "(none)"}",
            $"Provider binding: {instance.ProviderBindingId}",
            $"Policy binding: {instance.PolicyBindingId}",
            $"Projection freshness: {instance.Projection.Freshness}",
            $"Gateway: {instance.GatewayMode}/{instance.GatewayHealth}");
    }

    public static OperatorCommandResult PlatformSchedule(PlatformSchedulingDecision decision)
    {
        var lines = new List<string>
        {
            $"Requested slots: {decision.RequestedSlots}",
            $"Granted slots: {decision.GrantedSlots}",
            $"Reason: {decision.Reason}",
        };
        lines.AddRange(decision.Candidates.Select(candidate =>
            $"- {candidate.RepoId}: selected={candidate.Selected}; score={candidate.FairnessScore:0.00}; {candidate.Reason}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ProviderList(IReadOnlyList<ProviderDescriptor> providers)
    {
        var lines = new List<string> { "Providers:" };
        lines.AddRange(providers.Count == 0
            ? ["(none)"]
            : providers.Select(provider => $"- {provider.ProviderId}: profiles={provider.Profiles.Count}; worker_backends={provider.WorkerBackends.Count}; retries={provider.RetryLimit}; timeout={provider.TimeoutSeconds}s"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ProviderInspect(ProviderDescriptor provider)
    {
        var lines = new List<string>
        {
            $"Provider: {provider.ProviderId}",
            $"Display name: {provider.DisplayName}",
            $"Secret env var: {provider.SecretEnvironmentVariable}",
            $"Capabilities: planning={provider.Capabilities.SupportsPlanning}; codegen={provider.Capabilities.SupportsCodeGeneration}; structured={provider.Capabilities.SupportsStructuredOutput}",
            $"Permitted repo scopes: {(provider.PermittedRepoScopes.Count == 0 ? "(none)" : string.Join(", ", provider.PermittedRepoScopes))}",
            "Profiles:",
        };
        lines.AddRange(provider.Profiles.Select(profile => $"- {profile.ProfileId}: role={profile.Role}; model={profile.Model}; {profile.Description}"));
        lines.Add("Worker backends:");
        lines.AddRange(provider.WorkerBackends.Count == 0
            ? ["(none)"]
            : provider.WorkerBackends.Select(backend =>
                $"- {backend.BackendId}: routing={backend.RoutingIdentity}; protocol={backend.ProtocolFamily}/{backend.RequestFamily}; trust_profiles={string.Join(", ", backend.CompatibleTrustProfiles)}; health={backend.Health.State}; exec={backend.Capabilities.SupportsExecution}; json={backend.Capabilities.SupportsJsonMode}; system={backend.Capabilities.SupportsSystemPrompt}; tools={backend.Capabilities.SupportsToolCalls}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ProviderBound(RepoDescriptor descriptor)
    {
        return OperatorCommandResult.Success(
            $"Bound provider profile for {descriptor.RepoId}.",
            $"Provider profile: {descriptor.ProviderProfile}",
            $"Policy profile: {descriptor.PolicyProfile}");
    }

    public static OperatorCommandResult ProviderQuota(IReadOnlyList<ProviderQuotaSummaryDto> quotas)
    {
        var lines = new List<string> { "Provider quotas:" };
        lines.AddRange(quotas.Count == 0
            ? ["(none)"]
            : quotas.Select(quota => $"- {quota.ProfileId}: used={quota.UsedThisHour}/{quota.LimitPerHour}; remaining={quota.Remaining}; exhausted={quota.Exhausted}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ProviderRoute(ProviderRoutingDecision decision)
    {
        return OperatorCommandResult.Success(
            $"Repo: {decision.RepoId}",
            $"Role: {decision.Role}",
            $"Allowed: {decision.Allowed}",
            $"Used fallback: {decision.UsedFallback}",
            $"Provider: {decision.ProviderId ?? "(none)"}",
            $"Profile: {decision.ProfileId ?? "(none)"}",
            $"Denial: {decision.DenialReason}",
            $"Reason: {decision.Reason}",
            $"Quota: {(decision.QuotaEntry is null ? "(none)" : $"{decision.QuotaEntry.UsedThisHour}/{decision.QuotaEntry.LimitPerHour}")}");
    }
}
