using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    private static IReadOnlyList<string>? BuildFallbackBackendIds(WorkerSelectionRuntimePolicy? runtimeSelectionPolicy, RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        var values = new List<string>();
        if (routingProfileMatch is not null)
        {
            values.AddRange(routingProfileMatch.FallbackRoutes
                .Select(route => route.BackendId)
                .Where(backendId => !string.IsNullOrWhiteSpace(backendId))!
                .Cast<string>());
        }

        if (runtimeSelectionPolicy is not null)
        {
            values.AddRange(runtimeSelectionPolicy.FallbackBackendIds);
        }

        return values.Count == 0
            ? null
            : values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static RoutingContext ResolveRoutingContext(TaskNode? task, WorkerSelectionOptions? options)
    {
        if (task is null)
        {
            return new RoutingContext(options?.RoutingIntentOverride, options?.RoutingModuleIdOverride);
        }

        var metadata = task.Metadata;
        metadata.TryGetValue("routing_intent", out var routingIntent);
        metadata.TryGetValue("module_id", out var moduleId);
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            metadata.TryGetValue("affected_module", out moduleId);
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            moduleId = task.Scope.FirstOrDefault(scope => !string.IsNullOrWhiteSpace(scope));
        }

        return new RoutingContext(
            !string.IsNullOrWhiteSpace(options?.RoutingIntentOverride) ? options.RoutingIntentOverride : string.IsNullOrWhiteSpace(routingIntent) ? null : routingIntent,
            !string.IsNullOrWhiteSpace(options?.RoutingModuleIdOverride) ? options.RoutingModuleIdOverride : string.IsNullOrWhiteSpace(moduleId) ? null : moduleId);
    }

    private static MatchedRoute? ResolveMatchedRoute(WorkerBackendDescriptor backend, RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        if (routingProfileMatch is null)
        {
            return null;
        }

        if (RouteMatches(routingProfileMatch.PreferredRoute, backend))
        {
            return new MatchedRoute(routingProfileMatch.Rule, routingProfileMatch.PreferredRoute, "preferred");
        }

        var fallback = routingProfileMatch.FallbackRoutes.FirstOrDefault(route => RouteMatches(route, backend));
        return fallback is null ? null : new MatchedRoute(routingProfileMatch.Rule, fallback, "fallback");
    }

    private static bool RouteMatches(RuntimeRoutingRoute route, WorkerBackendDescriptor backend)
    {
        if (!string.IsNullOrWhiteSpace(route.BackendId)
            && !string.Equals(route.BackendId, backend.BackendId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(route.ProviderId)
            && !string.Equals(route.ProviderId, backend.ProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(route.RoutingProfileId)
            && !backend.RoutingProfiles.Contains(route.RoutingProfileId, StringComparer.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(route.RequestFamily)
            && !string.Equals(route.RequestFamily, backend.RequestFamily, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static MatchedRoute? ResolveAppliedRoute(CandidateState? selected, RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        if (selected is null || routingProfileMatch is null)
        {
            return null;
        }

        return ResolveMatchedRoute(selected.Backend, routingProfileMatch);
    }

    private static string ResolveRouteSource(RuntimeRoutingProfile? activeRoutingProfile, RuntimeRoutingRuleMatch? routingProfileMatch, MatchedRoute? appliedRoute, CandidateState? selected, WorkerSelectionOptions? options)
    {
        if (options?.IgnoreActiveRoutingProfile == true)
        {
            return "validation_baseline";
        }

        if (activeRoutingProfile is null)
        {
            return "no_active_profile";
        }

        if (routingProfileMatch is null)
        {
            return "active_profile_no_match";
        }

        if (appliedRoute is null)
        {
            return selected is null ? "active_profile_unresolved" : "active_profile_overridden";
        }

        return appliedRoute.Disposition switch
        {
            "preferred" => "active_profile_preferred",
            "fallback" => "active_profile_fallback",
            _ => "active_profile_overridden",
        };
    }

    private static string BuildRouteReason(RuntimeRoutingProfile? activeRoutingProfile, RuntimeRoutingRuleMatch? routingProfileMatch, MatchedRoute? appliedRoute, CandidateState? selected, WorkerSelectionOptions? options)
    {
        if (options?.IgnoreActiveRoutingProfile == true)
        {
            return "Validation baseline ignored the active routing profile and used a fixed lane.";
        }

        if (activeRoutingProfile is null)
        {
            return "No active routing profile is loaded.";
        }

        if (routingProfileMatch is null)
        {
            return $"Active routing profile '{activeRoutingProfile.ProfileId}' had no matching rule for this task.";
        }

        if (appliedRoute is null)
        {
            return selected is null
                ? $"Routing rule '{routingProfileMatch.Rule.RuleId}' matched but no eligible backend satisfied health/policy/capability gates."
                : $"Routing rule '{routingProfileMatch.Rule.RuleId}' matched but runtime used an existing fallback path after health/policy evaluation.";
        }

        return appliedRoute.Disposition switch
        {
            "preferred" => $"Routing rule '{appliedRoute.Rule.RuleId}' selected the preferred route.",
            "fallback" => options?.ForceFallbackOnly == true
                ? $"Routing rule '{appliedRoute.Rule.RuleId}' selected a configured fallback route because forced fallback mode was requested."
                : $"Routing rule '{appliedRoute.Rule.RuleId}' selected a configured fallback route.",
            _ => $"Routing rule '{appliedRoute.Rule.RuleId}' influenced selection.",
        };
    }

    private sealed record RoutingContext(string? RoutingIntent, string? ModuleId);

    private sealed record MatchedRoute(RuntimeRoutingRule Rule, RuntimeRoutingRoute Route, string Disposition);
}
