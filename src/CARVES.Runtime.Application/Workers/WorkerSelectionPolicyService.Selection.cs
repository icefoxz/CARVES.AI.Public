using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    public WorkerSelectionDecision Evaluate(TaskNode? task = null, string? repoId = null, bool allowFallback = true, WorkerSelectionOptions? options = null)
    {
        var descriptor = ResolveRepoDescriptor(repoId);
        var resolvedRepoId = descriptor?.RepoId ?? repoId ?? "local-repo";
        var repoPolicy = descriptor is null
            ? governanceService.ResolveRepoPolicy("balanced")
            : governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
        var providerPolicy = governanceService.ResolveProviderPolicy(repoPolicy.ProviderPolicyProfile);
        var requestedProfile = boundaryService.ResolveDesiredProfile(task, descriptor?.RepoId);
        var runtimeSelectionPolicy = runtimePolicyBundleService?.LoadWorkerSelectionPolicy();
        var activeRoutingProfile = options?.IgnoreActiveRoutingProfile == true ? null : runtimeRoutingProfileService?.LoadActive();
        var routingContext = ResolveRoutingContext(task, options);
        var routingProfileMatch = activeRoutingProfile is null ? null : runtimeRoutingProfileService?.Resolve(routingContext.RoutingIntent, routingContext.ModuleId);
        var explicitRequestedBackend = options?.RequestedBackendOverride ?? ResolveRequestedBackend(task);
        var routeRequestedBackend = routingProfileMatch?.PreferredRoute.BackendId;
        var routeRequestedRoutingProfileId = routingProfileMatch?.PreferredRoute.RoutingProfileId;
        var requestedBackend = explicitRequestedBackend ?? routeRequestedBackend ?? ResolvePreferredBackend(descriptor);
        var effectiveAllowFallback = allowFallback && (runtimeSelectionPolicy?.AllowRoutingFallback ?? true);
        var routing = descriptor is null
            ? null
            : providerRoutingService.Route(descriptor.RepoId, "worker", effectiveAllowFallback);

        var candidates = EvaluateCandidates(
            task,
            requestedProfile,
            providerPolicy,
            explicitRequestedBackend,
            requestedBackend,
            routeRequestedRoutingProfileId,
            routing,
            runtimeSelectionPolicy,
            routingProfileMatch,
            options);

        var exactRequested = !string.IsNullOrWhiteSpace(requestedBackend)
            ? candidates.FirstOrDefault(candidate =>
                candidate.IsPolicyEligible
                && string.Equals(candidate.Backend.BackendId, requestedBackend, StringComparison.OrdinalIgnoreCase))
            : null;
        CandidateState? selected;
        if (options?.ForceFallbackOnly == true)
        {
            selected = candidates.FirstOrDefault(candidate => candidate.IsSelectable && string.Equals(candidate.RouteDisposition, "fallback", StringComparison.Ordinal))
                ?? candidates.FirstOrDefault(candidate => candidate.IsPolicyEligible && string.Equals(candidate.RouteDisposition, "fallback", StringComparison.Ordinal));
        }
        else
        {
            selected = (descriptor is null || !string.IsNullOrWhiteSpace(explicitRequestedBackend))
                ? exactRequested
                : null;
            selected ??= candidates.FirstOrDefault(candidate => candidate.IsSelectable)
                ?? exactRequested
                ?? candidates.FirstOrDefault(candidate => candidate.IsPolicyEligible);
        }

        var appliedRoute = ResolveAppliedRoute(selected, routingProfileMatch);
        var routeSource = ResolveRouteSource(activeRoutingProfile, routingProfileMatch, appliedRoute, selected, options);
        var routeReason = BuildRouteReason(activeRoutingProfile, routingProfileMatch, appliedRoute, selected, options);
        var preferredCandidate = candidates.FirstOrDefault(candidate => string.Equals(candidate.RouteDisposition, "preferred", StringComparison.Ordinal));
        var preferredIneligibilityReason = preferredCandidate is not null && preferredCandidate.Eligibility != RouteEligibilityStatus.Eligible
            ? preferredCandidate.Projection.Reason
            : null;
        var selectedModelId = ResolveSelectedModelId(selected, appliedRoute);

        if (selected is null)
        {
            return BuildNoCompatibleWorkerDecision(
                resolvedRepoId,
                task,
                requestedProfile,
                activeRoutingProfile?.ProfileId,
                routingProfileMatch?.Rule.RuleId,
                routingContext.RoutingIntent,
                routingContext.ModuleId,
                selectedModelId ?? routingProfileMatch?.PreferredRoute.Model,
                appliedRoute,
                routeSource,
                routeReason,
                preferredCandidate,
                preferredIneligibilityReason,
                descriptor,
                candidates);
        }

        return BuildSelectionDecision(
            resolvedRepoId,
            task,
            requestedProfile,
            selected,
            activeRoutingProfile?.ProfileId,
            routingProfileMatch?.Rule.RuleId,
            routingContext.RoutingIntent,
            routingContext.ModuleId,
            selectedModelId,
            appliedRoute,
            routeSource,
            routeReason,
            preferredCandidate,
            preferredIneligibilityReason,
            options,
            candidates);
    }

    public WorkerSelectionCandidate? FindAlternative(TaskNode? task, string? currentBackendId, string? repoId = null)
    {
        var descriptor = ResolveRepoDescriptor(repoId);
        var repoPolicy = descriptor is null
            ? governanceService.ResolveRepoPolicy("balanced")
            : governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
        var providerPolicy = governanceService.ResolveProviderPolicy(repoPolicy.ProviderPolicyProfile);
        var requestedProfile = boundaryService.ResolveDesiredProfile(task, descriptor?.RepoId);
        var routingContext = ResolveRoutingContext(task, options: null);
        var routingProfileMatch = runtimeRoutingProfileService?.Resolve(routingContext.RoutingIntent, routingContext.ModuleId);
        var explicitRequestedBackend = ResolveRequestedBackend(task);
        var requestedBackend = explicitRequestedBackend ?? routingProfileMatch?.PreferredRoute.BackendId;
        var routing = descriptor is null ? null : providerRoutingService.Route(descriptor.RepoId, "worker", allowFallback: true);
        var alternatives = EvaluateCandidates(
                task,
                requestedProfile,
                providerPolicy,
                explicitRequestedBackend,
                requestedBackend,
                routingProfileMatch?.PreferredRoute.RoutingProfileId,
                routing,
                runtimePolicyBundleService?.LoadWorkerSelectionPolicy(),
                routingProfileMatch,
                options: null)
            .Where(candidate => !string.Equals(candidate.Backend.BackendId, currentBackendId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return alternatives
            .Where(candidate => candidate.IsSelectable)
            .Select(candidate => candidate.Projection)
            .FirstOrDefault()
            ?? alternatives
                .Where(candidate => candidate.IsPolicyEligible)
                .Select(candidate => candidate.Projection)
                .FirstOrDefault();
    }
}
