using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    private CandidateState BuildCandidate(
        WorkerBackendDescriptor backend,
        WorkerExecutionProfile requestedProfile,
        ProviderPolicy providerPolicy,
        TaskNode? task,
        WorkerSelectionRuntimePolicy? runtimeSelectionPolicy,
        string? explicitRequestedBackend,
        string? requestedBackend,
        string? requestedRoutingProfileId,
        ProviderRoutingDecision? routing,
        RuntimeRoutingRuleMatch? routingProfileMatch,
        WorkerSelectionOptions? options)
    {
        var adapter = workerAdapterRegistry.TryGetByBackendId(backend.BackendId);
        var currentHealth = providerHealthMonitorService.GetHealth(backend.BackendId);
        var healthSummary = currentHealth is null
            ? adapter?.CheckHealth() ?? backend.Health
            : new WorkerBackendHealthSummary
            {
                State = currentHealth.State,
                Summary = currentHealth.Summary,
                CheckedAt = currentHealth.CheckedAt,
                LatencyMs = currentHealth.LatencyMs,
                DegradationReason = currentHealth.DegradationReason,
                ConsecutiveFailureCount = currentHealth.ConsecutiveFailureCount,
            };
        var healthState = healthSummary.State.ToString();
        var matchedRoute = ResolveMatchedRoute(backend, routingProfileMatch);
        var allowMaterializedOverride = AllowsMaterializedRouteOverride(task, routingProfileMatch);
        var backendAllowedByRuntimePolicy = runtimeSelectionPolicy is null
            || runtimeSelectionPolicy.AllowedBackendIds is null
            || runtimeSelectionPolicy.AllowedBackendIds.Count == 0
            || runtimeSelectionPolicy.AllowedBackendIds.Contains(backend.BackendId, StringComparer.Ordinal);
        var allowRuntimePolicyRouteOverride = backendAllowedByRuntimePolicy
            && AllowsRuntimePolicyRouteOverride(runtimeSelectionPolicy, routingProfileMatch);
        var routeCompatible = routingProfileMatch is null
            || matchedRoute is not null
            || allowRuntimePolicyRouteOverride
            || (allowMaterializedOverride && SupportsMaterializedResultSubmission(backend))
            || (!string.IsNullOrWhiteSpace(explicitRequestedBackend)
                && string.Equals(explicitRequestedBackend, backend.BackendId, StringComparison.OrdinalIgnoreCase));
        var routingProfile = SelectRoutingProfile(
            backend,
            routing,
            providerPolicy,
            requestedRoutingProfileId,
            matchedRoute?.Route.RoutingProfileId);
        var providerPolicyAllowed = routeCompatible
            && (routingProfile is not null
                || (routing is null
                    && !string.IsNullOrWhiteSpace(explicitRequestedBackend)
                    && string.Equals(explicitRequestedBackend, backend.BackendId, StringComparison.OrdinalIgnoreCase)));
        var profileCompatible = backend.CompatibleTrustProfiles.Contains(requestedProfile.ProfileId, StringComparer.Ordinal);
        var capabilityCompatibility = EvaluateCapabilities(task, requestedProfile, backend, backend.Capabilities, out var capabilityReason);
        var healthCompatible = healthSummary.State is WorkerBackendHealthState.Healthy or WorkerBackendHealthState.Degraded;
        var exactBackendRequested = !string.IsNullOrWhiteSpace(requestedBackend)
            && string.Equals(requestedBackend, backend.BackendId, StringComparison.OrdinalIgnoreCase);
        var quotaState = ResolveQuotaState(routing);
        var forcePreferredIneligible = options?.ForceFallbackOnly == true && string.Equals(matchedRoute?.Disposition, "preferred", StringComparison.Ordinal);
        var locallyConfigured = adapter is null || !adapter.IsRealAdapter || adapter.IsConfigured;
        var policyEligible = backendAllowedByRuntimePolicy && providerPolicyAllowed && profileCompatible && capabilityCompatibility && adapter is not null && locallyConfigured;
        var signals = new RouteSelectionSignals
        {
            RouteHealth = healthState,
            QuotaState = quotaState,
            TokenBudgetFit = EstimateTokenBudgetFit(task, backend),
            RecentLatencyMs = healthSummary.LatencyMs,
            RecentFailureCount = healthSummary.ConsecutiveFailureCount,
        };
        var eligibility = DetermineEligibility(
            adapter,
            routeCompatible,
            providerPolicyAllowed,
            profileCompatible,
            capabilityCompatibility,
            quotaState,
            healthCompatible,
            forcePreferredIneligible);
        var selectable = eligibility == RouteEligibilityStatus.Eligible;
        var usedFallback = (routing is not null
                && routing.Allowed
                && !string.IsNullOrWhiteSpace(routing.ProfileId)
                && !backend.RoutingProfiles.Contains(routing.ProfileId, StringComparer.Ordinal)
                && policyEligible)
            || string.Equals(matchedRoute?.Disposition, "fallback", StringComparison.Ordinal);
        var routeDisposition = matchedRoute?.Disposition ?? "none";
        var reason = BuildCandidateReason(
            selectable,
            policyEligible,
            adapter,
            backendAllowedByRuntimePolicy,
            routeCompatible,
            providerPolicyAllowed,
            profileCompatible,
            capabilityCompatibility,
            capabilityReason,
            locallyConfigured,
            healthCompatible,
            exactBackendRequested,
            requestedProfile.ProfileId,
            healthSummary.Summary,
            quotaState,
            routeDisposition,
            matchedRoute?.Rule.RuleId,
            forcePreferredIneligible);

        return new CandidateState(
            backend,
            adapter,
            selectable,
            policyEligible,
            usedFallback,
            healthCompatible,
            eligibility,
            routeDisposition,
            routingProfile,
            new WorkerSelectionCandidate
            {
                BackendId = backend.BackendId,
                ProviderId = backend.ProviderId,
                AdapterId = adapter?.AdapterId ?? backend.AdapterId,
                RoutingProfileId = routingProfile,
                RoutingRuleId = matchedRoute?.Rule.RuleId,
                RouteDisposition = routeDisposition,
                HealthState = healthState,
                ProfileCompatible = profileCompatible,
                CapabilityCompatible = capabilityCompatibility,
                Eligibility = eligibility,
                Signals = signals,
                Selected = false,
                Reason = reason,
            });
    }

    private bool EvaluateCapabilities(
        TaskNode? task,
        WorkerExecutionProfile requestedProfile,
        WorkerBackendDescriptor backend,
        WorkerProviderCapabilities capabilities,
        out string reason)
    {
        if (!capabilities.SupportsExecution)
        {
            reason = "backend does not support execution";
            return false;
        }

        if (requestedProfile.NetworkAccessEnabled && !capabilities.SupportsNetworkAccess)
        {
            reason = "backend does not support required network access";
            return false;
        }

        if (requestedProfile.Trusted && !capabilities.SupportsTrustedProfiles)
        {
            reason = "backend does not support trusted profiles";
            return false;
        }

        var qualificationDecision = remoteWorkerQualificationService.Evaluate(task, backend);
        if (!qualificationDecision.Allowed)
        {
            reason = qualificationDecision.Summary;
            return false;
        }

        if (task is not null
            && RequiresMaterializedResultSubmission(task)
            && !SupportsMaterializedResultSubmission(backend))
        {
            reason = "backend does not support materialized patch/result submission for this execution task";
            return false;
        }

        if (task is not null
            && task.Validation.Commands.Select(command => string.Join(' ', command))
                .Any(command => command.StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
            && !capabilities.SupportsDotNetBuild)
        {
            reason = "backend does not support dotnet build/test validation";
            return false;
        }

        if (task is not null
            && task.Capabilities.Contains("long_running_tests", StringComparer.OrdinalIgnoreCase)
            && !capabilities.SupportsLongRunningTasks)
        {
            reason = "backend does not support long-running task requirements";
            return false;
        }

        reason = "capabilities satisfied";
        return true;
    }

    private static bool AllowsMaterializedRouteOverride(TaskNode? task, RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        if (!RequiresMaterializedResultSubmission(task) || routingProfileMatch is null)
        {
            return false;
        }

        if (RouteSupportsMaterializedResultSubmission(routingProfileMatch.PreferredRoute))
        {
            return false;
        }

        return !routingProfileMatch.FallbackRoutes.Any(RouteSupportsMaterializedResultSubmission);
    }

    private static bool AllowsRuntimePolicyRouteOverride(
        WorkerSelectionRuntimePolicy? runtimeSelectionPolicy,
        RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        if (runtimeSelectionPolicy?.AllowedBackendIds is null
            || runtimeSelectionPolicy.AllowedBackendIds.Count == 0
            || routingProfileMatch is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(routingProfileMatch.PreferredRoute.BackendId)
            && runtimeSelectionPolicy.AllowedBackendIds.Contains(routingProfileMatch.PreferredRoute.BackendId, StringComparer.Ordinal))
        {
            return false;
        }

        if (routingProfileMatch.FallbackRoutes.Any(route =>
                !string.IsNullOrWhiteSpace(route.BackendId)
                && runtimeSelectionPolicy.AllowedBackendIds.Contains(route.BackendId, StringComparer.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool RequiresMaterializedResultSubmission(TaskNode? task)
    {
        if (task is null || !task.CanExecuteInWorker)
        {
            return false;
        }

        if (task.Metadata.TryGetValue("routing_intent", out var routingIntent))
        {
            if (routingIntent is "failure_summary" or "reasoning_summary" or "review_summary" or "structured_output")
            {
                return false;
            }

            if (string.Equals(routingIntent, "patch_draft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return task.Metadata.TryGetValue("requires_materialized_result_submission", out var requiresMaterializedSubmission)
               && bool.TryParse(requiresMaterializedSubmission, out var parsed)
               && parsed;
    }

    private static bool SupportsMaterializedResultSubmission(WorkerBackendDescriptor backend)
    {
        return backend.ProtocolFamily is "local_cli" or "sdk_bridge" or "local_bridge";
    }

    private static bool RouteSupportsMaterializedResultSubmission(RuntimeRoutingRoute route)
    {
        if (!string.IsNullOrWhiteSpace(route.BackendId))
        {
            return route.BackendId is "codex_cli" or "codex_sdk" or "local_agent";
        }

        return route.RequestFamily is "codex_exec" or "codex_sdk" or "local_agent";
    }

    private static bool IsControlPlaneAssessmentScope(string scopePath)
    {
        var normalized = scopePath
            .Trim()
            .Trim('`')
            .Replace('\\', '/');
        return normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, ".ai", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("carves://truth/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCandidateReason(
        bool selectable,
        bool policyEligible,
        IWorkerAdapter? adapter,
        bool backendAllowedByRuntimePolicy,
        bool routeCompatible,
        bool providerPolicyAllowed,
        bool profileCompatible,
        bool capabilityCompatibility,
        string capabilityReason,
        bool locallyConfigured,
        bool healthCompatible,
        bool exactBackendRequested,
        string requestedProfileId,
        string healthSummary,
        RouteQuotaState quotaState,
        string routeDisposition,
        string? routingRuleId,
        bool forcePreferredIneligible)
    {
        if (adapter is null)
        {
            return "no runtime adapter is registered for this backend";
        }

        if (!backendAllowedByRuntimePolicy)
        {
            return "runtime worker policy currently restricts execution to allowed_backends only";
        }

        if (!routeCompatible)
        {
            return "active routing rule does not allow this backend/request-family combination";
        }

        if (!providerPolicyAllowed)
        {
            return "provider routing policy does not allow this backend";
        }

        if (!profileCompatible)
        {
            return $"trust profile '{requestedProfileId}' is not compatible with this backend";
        }

        if (!capabilityCompatibility)
        {
            return capabilityReason;
        }

        if (!locallyConfigured)
        {
            return "runtime adapter is not locally configured";
        }

        if (quotaState == RouteQuotaState.Exhausted)
        {
            return "provider quota is exhausted for the current route";
        }

        if (forcePreferredIneligible)
        {
            return "forced fallback requested; preferred route withheld from selection";
        }

        if (!healthCompatible)
        {
            return policyEligible
                ? $"selected only as a degraded fallback because {healthSummary.ToLowerInvariant()}"
                : healthSummary;
        }

        if (!string.Equals(routeDisposition, "none", StringComparison.Ordinal))
        {
            return $"matched {routeDisposition} route{(string.IsNullOrWhiteSpace(routingRuleId) ? string.Empty : $" via rule '{routingRuleId}'")}";
        }

        if (selectable && exactBackendRequested)
        {
            return "selected because the task requested this backend";
        }

        return selectable ? "backend is eligible for selection" : "backend is not selectable";
    }
}
