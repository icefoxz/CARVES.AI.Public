using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeRoutingProfileService
{
    private readonly IRuntimeRoutingProfileRepository repository;
    private readonly ICurrentModelQualificationRepository? qualificationRepository;

    public RuntimeRoutingProfileService(
        IRuntimeRoutingProfileRepository repository,
        ICurrentModelQualificationRepository? qualificationRepository = null)
    {
        this.repository = repository;
        this.qualificationRepository = qualificationRepository;
    }

    public RuntimeRoutingProfile? LoadActive()
    {
        return EnrichLegacyRoutes(repository.LoadActive());
    }

    public RuntimeRoutingRuleMatch? Resolve(string? routingIntent, string? moduleId)
    {
        var profile = LoadActive();
        if (profile is null)
        {
            return null;
        }

        var match = profile.Rules
            .Select(rule => new
            {
                Rule = rule,
                Score = Score(rule, routingIntent, moduleId),
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Rule.RuleId, StringComparer.Ordinal)
            .Select(item => item.Rule)
            .FirstOrDefault();

        return match is null
            ? null
            : new RuntimeRoutingRuleMatch(match, match.PreferredRoute, match.FallbackRoutes);
    }

    private static int Score(RuntimeRoutingRule rule, string? routingIntent, string? moduleId)
    {
        if (!Matches(rule.RoutingIntent, routingIntent) || !Matches(rule.ModuleId, moduleId))
        {
            return -1;
        }

        var score = 0;
        if (!string.IsNullOrWhiteSpace(rule.RoutingIntent))
        {
            score += 2;
        }

        if (!string.IsNullOrWhiteSpace(rule.ModuleId))
        {
            score += 4;
        }

        return score;
    }

    private static bool Matches(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private RuntimeRoutingProfile? EnrichLegacyRoutes(RuntimeRoutingProfile? profile)
    {
        if (profile is null)
        {
            return null;
        }

        var matrix = qualificationRepository?.LoadMatrix();
        if (matrix is null || profile.Rules.All(rule => HasRequestFamily(rule.PreferredRoute) && rule.FallbackRoutes.All(HasRequestFamily)))
        {
            return profile;
        }

        var changed = false;
        var rules = profile.Rules.Select(rule =>
        {
            var preferred = EnrichRoute(rule.PreferredRoute, matrix, ref changed);
            var fallbacks = rule.FallbackRoutes.Select(route => EnrichRoute(route, matrix, ref changed)).ToArray();
            if (ReferenceEquals(preferred, rule.PreferredRoute) && fallbacks.SequenceEqual(rule.FallbackRoutes))
            {
                return rule;
            }

            return new RuntimeRoutingRule
            {
                RuleId = rule.RuleId,
                RoutingIntent = rule.RoutingIntent,
                ModuleId = rule.ModuleId,
                Summary = rule.Summary,
                PreferredRoute = preferred,
                FallbackRoutes = fallbacks,
            };
        }).ToArray();

        if (!changed)
        {
            return profile;
        }

        return new RuntimeRoutingProfile
        {
            SchemaVersion = profile.SchemaVersion,
            ProfileId = profile.ProfileId,
            Version = profile.Version,
            SourceQualificationId = profile.SourceQualificationId,
            Summary = profile.Summary,
            CreatedAt = profile.CreatedAt,
            ActivatedAt = profile.ActivatedAt,
            Rules = rules,
        };
    }

    private static RuntimeRoutingRoute EnrichRoute(RuntimeRoutingRoute route, ModelQualificationMatrix matrix, ref bool changed)
    {
        if (HasRequestFamily(route))
        {
            return route;
        }

        var lane = matrix.Lanes.FirstOrDefault(item =>
                       string.Equals(item.ProviderId, route.ProviderId, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(item.BackendId, route.BackendId, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(item.Model, route.Model, StringComparison.Ordinal))
                   ?? matrix.Lanes.FirstOrDefault(item =>
                       string.Equals(item.ProviderId, route.ProviderId, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(item.BackendId, route.BackendId, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(item.RoutingProfileId, route.RoutingProfileId, StringComparison.Ordinal));
        if (lane is null || string.IsNullOrWhiteSpace(lane.RequestFamily))
        {
            return route;
        }

        changed = true;
        return new RuntimeRoutingRoute
        {
            ProviderId = route.ProviderId,
            BackendId = route.BackendId,
            RoutingProfileId = route.RoutingProfileId,
            RequestFamily = lane.RequestFamily,
            BaseUrl = string.IsNullOrWhiteSpace(route.BaseUrl) ? lane.BaseUrl : route.BaseUrl,
            ApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(route.ApiKeyEnvironmentVariable) ? lane.ApiKeyEnvironmentVariable : route.ApiKeyEnvironmentVariable,
            Model = route.Model,
        };
    }

    private static bool HasRequestFamily(RuntimeRoutingRoute route)
    {
        return !string.IsNullOrWhiteSpace(route.RequestFamily);
    }
}
