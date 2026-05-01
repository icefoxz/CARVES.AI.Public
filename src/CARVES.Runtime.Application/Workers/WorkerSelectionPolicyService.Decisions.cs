using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    private static WorkerSelectionDecision BuildNoCompatibleWorkerDecision(
        string resolvedRepoId,
        TaskNode? task,
        WorkerExecutionProfile requestedProfile,
        string? activeRoutingProfileId,
        string? appliedRoutingRuleId,
        string? routingIntent,
        string? routingModuleId,
        string? selectedModelId,
        MatchedRoute? appliedRoute,
        string routeSource,
        string routeReason,
        CandidateState? preferredCandidate,
        string? preferredIneligibilityReason,
        RepoDescriptor? descriptor,
        IReadOnlyList<CandidateState> candidates)
    {
        return new WorkerSelectionDecision
        {
            RepoId = resolvedRepoId,
            TaskId = task?.TaskId,
            Allowed = false,
            UsedFallback = false,
            RequestedTrustProfileId = requestedProfile.ProfileId,
            ActiveRoutingProfileId = activeRoutingProfileId,
            AppliedRoutingRuleId = appliedRoutingRuleId,
            RoutingIntent = routingIntent,
            RoutingModuleId = routingModuleId,
            SelectedModelId = selectedModelId,
            SelectedRequestFamily = appliedRoute?.Route.RequestFamily,
            SelectedBaseUrl = appliedRoute?.Route.BaseUrl,
            SelectedApiKeyEnvironmentVariable = appliedRoute?.Route.ApiKeyEnvironmentVariable,
            RouteSource = routeSource,
            RouteReason = routeReason,
            PreferredRouteEligibility = preferredCandidate?.Eligibility,
            PreferredIneligibilityReason = preferredIneligibilityReason,
            Summary = descriptor is null
                ? $"No worker backend can satisfy trust profile '{requestedProfile.ProfileId}'."
                : $"No worker backend can satisfy repo '{descriptor.RepoId}' with trust profile '{requestedProfile.ProfileId}'.",
            ReasonCode = "no_compatible_worker",
            Profile = requestedProfile,
            Candidates = candidates.Select(item => item.Projection).ToArray(),
        };
    }

    private WorkerSelectionDecision BuildSelectionDecision(
        string resolvedRepoId,
        TaskNode? task,
        WorkerExecutionProfile requestedProfile,
        CandidateState selected,
        string? activeRoutingProfileId,
        string? fallbackRoutingRuleId,
        string? routingIntent,
        string? routingModuleId,
        string? selectedModelId,
        MatchedRoute? appliedRoute,
        string routeSource,
        string routeReason,
        CandidateState? preferredCandidate,
        string? preferredIneligibilityReason,
        WorkerSelectionOptions? options,
        IReadOnlyList<CandidateState> candidates)
    {
        var providerDescriptor = providerRegistryService.List()
            .FirstOrDefault(item => string.Equals(item.ProviderId, selected.Backend.ProviderId, StringComparison.Ordinal));
        return new WorkerSelectionDecision
        {
            RepoId = resolvedRepoId,
            TaskId = task?.TaskId,
            Allowed = true,
            UsedFallback = selected.UsedFallback || string.Equals(appliedRoute?.Disposition, "fallback", StringComparison.Ordinal),
            RequestedTrustProfileId = requestedProfile.ProfileId,
            SelectedBackendId = selected.Backend.BackendId,
            SelectedProviderId = selected.Backend.ProviderId,
            SelectedAdapterId = selected.Adapter?.AdapterId ?? selected.Backend.AdapterId,
            SelectedRoutingProfileId = selected.SelectedRoutingProfile,
            ActiveRoutingProfileId = activeRoutingProfileId,
            AppliedRoutingRuleId = appliedRoute?.Rule.RuleId ?? fallbackRoutingRuleId,
            RoutingIntent = routingIntent,
            RoutingModuleId = routingModuleId,
            SelectedModelId = selectedModelId,
            SelectedRequestFamily = appliedRoute?.Route.RequestFamily,
            SelectedBaseUrl = appliedRoute?.Route.BaseUrl,
            SelectedApiKeyEnvironmentVariable = appliedRoute?.Route.ApiKeyEnvironmentVariable,
            SelectedProviderTimeoutSeconds = providerDescriptor?.TimeoutSeconds,
            SelectedProviderRetryLimit = providerDescriptor?.RetryLimit,
            SelectedBackendSupportsLongRunningTasks = selected.Backend.Capabilities.SupportsLongRunningTasks,
            RouteSource = routeSource,
            RouteReason = routeReason,
            PreferredRouteEligibility = preferredCandidate?.Eligibility,
            PreferredIneligibilityReason = preferredIneligibilityReason,
            Summary = selected.UsedFallback
                ? $"Selected fallback worker backend '{selected.Backend.BackendId}' with trust profile '{requestedProfile.ProfileId}'."
                : selected.HealthCompatible
                    ? $"Selected worker backend '{selected.Backend.BackendId}' with trust profile '{requestedProfile.ProfileId}'."
                    : $"Selected worker backend '{selected.Backend.BackendId}' with trust profile '{requestedProfile.ProfileId}' despite backend health '{selected.Backend.Health.State}'.",
            ReasonCode = selected.UsedFallback
                ? "fallback_selected"
                : selected.HealthCompatible
                    ? "selected"
                    : "selected_with_unhealthy_backend",
            SelectedBecause = BuildSelectedBecause(selected, appliedRoute, options),
            Profile = requestedProfile,
            Candidates = ProjectCandidates(candidates, selected.Backend.BackendId),
        };
    }

    private static WorkerSelectionCandidate[] ProjectCandidates(IReadOnlyList<CandidateState> candidates, string selectedBackendId)
    {
        return candidates.Select(item => new WorkerSelectionCandidate
        {
            BackendId = item.Projection.BackendId,
            ProviderId = item.Projection.ProviderId,
            AdapterId = item.Projection.AdapterId,
            RoutingProfileId = item.Projection.RoutingProfileId,
            RoutingRuleId = item.Projection.RoutingRuleId,
            RouteDisposition = item.Projection.RouteDisposition,
            HealthState = item.Projection.HealthState,
            ProfileCompatible = item.Projection.ProfileCompatible,
            CapabilityCompatible = item.Projection.CapabilityCompatible,
            Selected = string.Equals(item.Backend.BackendId, selectedBackendId, StringComparison.Ordinal),
            Eligibility = item.Projection.Eligibility,
            Signals = item.Projection.Signals,
            Reason = item.Projection.Reason,
        }).ToArray();
    }
}
