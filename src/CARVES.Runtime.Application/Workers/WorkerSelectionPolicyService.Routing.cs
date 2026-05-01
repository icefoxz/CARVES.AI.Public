using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    private static string? SelectRoutingProfile(
        WorkerBackendDescriptor backend,
        ProviderRoutingDecision? routing,
        ProviderPolicy providerPolicy,
        string? requestedRoutingProfileId,
        string? matchedRouteRoutingProfileId)
    {
        if (!string.IsNullOrWhiteSpace(matchedRouteRoutingProfileId)
            && backend.RoutingProfiles.Contains(matchedRouteRoutingProfileId, StringComparer.Ordinal)
            && providerPolicy.AllowedProviderProfiles.Contains(matchedRouteRoutingProfileId, StringComparer.Ordinal))
        {
            return matchedRouteRoutingProfileId;
        }

        if (!string.IsNullOrWhiteSpace(requestedRoutingProfileId)
            && backend.RoutingProfiles.Contains(requestedRoutingProfileId, StringComparer.Ordinal)
            && providerPolicy.AllowedProviderProfiles.Contains(requestedRoutingProfileId, StringComparer.Ordinal))
        {
            return requestedRoutingProfileId;
        }

        if (routing is not null
            && routing.Allowed
            && !string.IsNullOrWhiteSpace(routing.ProfileId)
            && backend.RoutingProfiles.Contains(routing.ProfileId, StringComparer.Ordinal))
        {
            return routing.ProfileId;
        }

        return backend.RoutingProfiles.FirstOrDefault(profile =>
            providerPolicy.AllowedProviderProfiles.Contains(profile, StringComparer.Ordinal));
    }

    private static int ScoreCandidate(WorkerBackendDescriptor backend, TaskNode? task, string? requestedBackend, string? routedProfileId, IReadOnlyList<string>? fallbackBackendIds)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(requestedBackend) && string.Equals(requestedBackend, backend.BackendId, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(routedProfileId) && backend.RoutingProfiles.Contains(routedProfileId, StringComparer.Ordinal))
        {
            score += 50;
        }

        if (fallbackBackendIds is not null && fallbackBackendIds.Contains(backend.BackendId, StringComparer.Ordinal))
        {
            score += 20;
        }

        if (backend.Health.State == WorkerBackendHealthState.Healthy)
        {
            score += 10;
        }

        if (backend.Health.State == WorkerBackendHealthState.Degraded)
        {
            score += 5;
        }

        if (RequiresMaterializedResultSubmission(task))
        {
            score += backend.BackendId switch
            {
                "codex_cli" => 30,
                "codex_sdk" => 20,
                _ when SupportsMaterializedResultSubmission(backend) => 10,
                _ => 0,
            };
        }

        return score;
    }

    private RepoDescriptor? ResolveRepoDescriptor(string? repoId)
    {
        if (!string.IsNullOrWhiteSpace(repoId))
        {
            return repoRegistryService.List().FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));
        }

        return repoRegistryService.List().FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveRequestedBackend(TaskNode? task)
    {
        return task is null
            ? null
            : task.Metadata.TryGetValue("worker_backend", out var backendId)
                ? backendId
                : null;
    }

    private string? ResolvePreferredBackend(RepoDescriptor? descriptor)
    {
        var runtimeSelectionPolicy = runtimePolicyBundleService?.LoadWorkerSelectionPolicy();
        if (descriptor is null)
        {
            if (!string.IsNullOrWhiteSpace(runtimeSelectionPolicy?.PreferredBackendId)
                && !string.Equals(runtimeSelectionPolicy.PreferredBackendId, "null_worker", StringComparison.OrdinalIgnoreCase))
            {
                return runtimeSelectionPolicy.PreferredBackendId;
            }

            return workerAdapterRegistry.ActiveAdapter.BackendId;
        }

        if (!string.IsNullOrWhiteSpace(runtimeSelectionPolicy?.PreferredBackendId))
        {
            return runtimeSelectionPolicy.PreferredBackendId;
        }

        return operationalPolicyService.ResolvePreferredBackendId(descriptor.RepoId);
    }

    private string? ResolveSelectedModelId(CandidateState? selected, MatchedRoute? appliedRoute)
    {
        if (!string.IsNullOrWhiteSpace(appliedRoute?.Route.Model))
        {
            return appliedRoute.Route.Model;
        }

        return providerRegistryService.ResolveProfileModel(selected?.Backend.ProviderId, selected?.SelectedRoutingProfile);
    }

    private CandidateState[] EvaluateCandidates(
        TaskNode? task,
        WorkerExecutionProfile requestedProfile,
        ProviderPolicy providerPolicy,
        string? explicitRequestedBackend,
        string? requestedBackend,
        string? requestedRoutingProfileId,
        ProviderRoutingDecision? routing,
        WorkerSelectionRuntimePolicy? runtimeSelectionPolicy,
        RuntimeRoutingRuleMatch? routingProfileMatch,
        WorkerSelectionOptions? options)
    {
        var fallbackBackendIds = BuildFallbackBackendIds(runtimeSelectionPolicy, routingProfileMatch);
        return providerRegistryService.ListWorkerBackends()
            .OrderBy(item => ScoreCandidate(item, task, requestedBackend, requestedRoutingProfileId ?? routing?.ProfileId, fallbackBackendIds), Comparer<int>.Create((left, right) => right.CompareTo(left)))
            .ThenBy(item => item.BackendId, StringComparer.Ordinal)
            .Select(item => BuildCandidate(item, requestedProfile, providerPolicy, task, runtimeSelectionPolicy, explicitRequestedBackend, requestedBackend, requestedRoutingProfileId, routing, routingProfileMatch, options))
            .ToArray();
    }

    private sealed record CandidateState(
        WorkerBackendDescriptor Backend,
        IWorkerAdapter? Adapter,
        bool IsSelectable,
        bool IsPolicyEligible,
        bool UsedFallback,
        bool HealthCompatible,
        RouteEligibilityStatus Eligibility,
        string RouteDisposition,
        string? SelectedRoutingProfile,
        WorkerSelectionCandidate Projection);

    private static RouteQuotaState ResolveQuotaState(ProviderRoutingDecision? routing)
    {
        if (routing is null)
        {
            return RouteQuotaState.Unknown;
        }

        if (routing.DenialReason == ProviderRoutingDenialReason.QuotaExhausted)
        {
            return RouteQuotaState.Exhausted;
        }

        return routing.Allowed ? RouteQuotaState.Healthy : RouteQuotaState.Unknown;
    }

    private static RouteEligibilityStatus DetermineEligibility(
        IWorkerAdapter? adapter,
        bool routeCompatible,
        bool providerPolicyAllowed,
        bool profileCompatible,
        bool capabilityCompatibility,
        RouteQuotaState quotaState,
        bool healthCompatible,
        bool forcePreferredIneligible)
    {
        if (adapter is null || !routeCompatible || !providerPolicyAllowed || !profileCompatible || !capabilityCompatibility)
        {
            return RouteEligibilityStatus.Unsupported;
        }

        if (quotaState == RouteQuotaState.Exhausted)
        {
            return RouteEligibilityStatus.Exhausted;
        }

        if (forcePreferredIneligible || !healthCompatible)
        {
            return RouteEligibilityStatus.TemporarilyIneligible;
        }

        return RouteEligibilityStatus.Eligible;
    }

    private static bool EstimateTokenBudgetFit(TaskNode? task, WorkerBackendDescriptor backend)
    {
        if (task is null)
        {
            return true;
        }

        var approximatePromptSize =
            (task.Title?.Length ?? 0)
            + (task.Description?.Length ?? 0)
            + task.Acceptance.Sum(item => item.Length)
            + task.Scope.Sum(item => item.Length)
            + task.Validation.Commands.Sum(command => string.Join(' ', command).Length);
        var limit = backend.Capabilities.SupportsLongRunningTasks ? 12000 : 6000;
        return approximatePromptSize <= limit;
    }

    private static IReadOnlyList<string> BuildSelectedBecause(CandidateState selected, MatchedRoute? appliedRoute, WorkerSelectionOptions? options)
    {
        var reasons = new List<string>();
        if (options?.IgnoreActiveRoutingProfile == true)
        {
            reasons.Add("baseline_fixed_lane");
        }

        if (appliedRoute is not null)
        {
            reasons.Add(appliedRoute.Disposition == "fallback" ? "fallback_route_selected" : "preferred_route_eligible");
        }

        if (selected.Projection.Signals.TokenBudgetFit)
        {
            reasons.Add("within_token_budget");
        }

        if (selected.Projection.Signals.QuotaState == RouteQuotaState.Healthy)
        {
            reasons.Add("quota_healthy");
        }

        if (selected.Projection.Signals.RecentLatencyMs is not null)
        {
            reasons.Add("latency_observed");
        }

        return reasons;
    }

}
