using System.Text.Json.Nodes;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildWorkerApprovals()
    {
        var requests = services.OperatorApiService.GetPendingWorkerPermissionRequests();
        return new JsonObject
        {
            ["kind"] = "worker_approvals",
            ["pending_count"] = requests.Count,
            ["requests"] = new JsonArray(requests.Take(10).Select(request => new JsonObject
            {
                ["permission_request_id"] = request.PermissionRequestId,
                ["task_id"] = request.TaskId,
                ["backend_id"] = request.BackendId,
                ["provider_id"] = request.ProviderId,
                ["kind"] = request.Kind.ToString(),
                ["risk_level"] = request.RiskLevel.ToString(),
                ["scope_summary"] = request.ScopeSummary,
                ["resource_path"] = request.ResourcePath,
                ["summary"] = request.Summary,
                ["recommended_decision"] = request.RecommendedDecision.ToString(),
                ["recommended_reason"] = request.RecommendedReason,
                ["recommended_consequence"] = request.RecommendedConsequenceSummary,
            }).ToArray()),
        };
    }

    public JsonObject BuildWorkerSelectionSummary()
    {
        var repoId = services.OperatorApiService.GetPlatformStatus().Repos.FirstOrDefault()?.RepoId ?? "local-repo";
        var selection = services.OperatorApiService.GetWorkerSelection(repoId, null);
        return new JsonObject
        {
            ["kind"] = "worker_selection",
            ["repo_id"] = repoId,
            ["allowed"] = selection.Allowed,
            ["backend_id"] = selection.SelectedBackendId,
            ["provider_id"] = selection.SelectedProviderId,
            ["model"] = selection.SelectedModelId,
            ["trust_profile_id"] = selection.RequestedTrustProfileId,
            ["active_routing_profile_id"] = selection.ActiveRoutingProfileId,
            ["selected_routing_profile_id"] = selection.SelectedRoutingProfileId,
            ["routing_rule_id"] = selection.AppliedRoutingRuleId,
            ["routing_intent"] = selection.RoutingIntent,
            ["routing_module_id"] = selection.RoutingModuleId,
            ["route_source"] = selection.RouteSource,
            ["route_reason"] = selection.RouteReason,
            ["summary"] = selection.Summary,
            ["reason_code"] = selection.ReasonCode,
            ["candidates"] = new JsonArray(selection.Candidates.Select(candidate => new JsonObject
            {
                ["backend_id"] = candidate.BackendId,
                ["provider_id"] = candidate.ProviderId,
                ["routing_profile_id"] = candidate.RoutingProfileId,
                ["routing_rule_id"] = candidate.RoutingRuleId,
                ["route_disposition"] = candidate.RouteDisposition,
                ["health_state"] = candidate.HealthState,
                ["profile_compatible"] = candidate.ProfileCompatible,
                ["capability_compatible"] = candidate.CapabilityCompatible,
                ["selected"] = candidate.Selected,
                ["reason"] = candidate.Reason,
            }).ToArray()),
        };
    }

    public JsonObject BuildRoutingProfileSummary()
    {
        var profile = services.OperatorApiService.GetActiveRoutingProfile();
        return new JsonObject
        {
            ["kind"] = "routing_profile",
            ["active"] = profile is not null,
            ["profile"] = profile is null
                ? null
                : new JsonObject
                {
                    ["profile_id"] = profile.ProfileId,
                    ["version"] = profile.Version,
                    ["source_qualification_id"] = profile.SourceQualificationId,
                    ["summary"] = profile.Summary,
                    ["created_at"] = profile.CreatedAt,
                    ["activated_at"] = profile.ActivatedAt,
                    ["rules"] = new JsonArray(profile.Rules.Select(rule => (JsonNode)new JsonObject
                    {
                        ["rule_id"] = rule.RuleId,
                        ["routing_intent"] = rule.RoutingIntent,
                        ["module_id"] = rule.ModuleId,
                        ["summary"] = rule.Summary,
                        ["preferred_route"] = new JsonObject
                        {
                            ["provider_id"] = rule.PreferredRoute.ProviderId,
                            ["backend_id"] = rule.PreferredRoute.BackendId,
                            ["routing_profile_id"] = rule.PreferredRoute.RoutingProfileId,
                            ["request_family"] = rule.PreferredRoute.RequestFamily,
                            ["base_url"] = rule.PreferredRoute.BaseUrl,
                            ["api_key_environment_variable"] = rule.PreferredRoute.ApiKeyEnvironmentVariable,
                            ["model"] = rule.PreferredRoute.Model,
                        },
                        ["fallback_routes"] = new JsonArray(rule.FallbackRoutes.Select(route => (JsonNode)new JsonObject
                        {
                            ["provider_id"] = route.ProviderId,
                            ["backend_id"] = route.BackendId,
                            ["routing_profile_id"] = route.RoutingProfileId,
                            ["request_family"] = route.RequestFamily,
                            ["base_url"] = route.BaseUrl,
                            ["api_key_environment_variable"] = route.ApiKeyEnvironmentVariable,
                            ["model"] = route.Model,
                        }).ToArray()),
                    }).ToArray()),
                },
        };
    }
}
