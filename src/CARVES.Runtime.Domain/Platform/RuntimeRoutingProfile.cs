namespace Carves.Runtime.Domain.Platform;

public sealed class RuntimeRoutingProfile
{
    public string SchemaVersion { get; init; } = "runtime-routing-profile.v1";

    public string ProfileId { get; init; } = string.Empty;

    public string Version { get; init; } = "1";

    public string? SourceQualificationId { get; init; }

    public string? Summary { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ActivatedAt { get; init; }

    public RuntimeRoutingRule[] Rules { get; init; } = [];
}

public sealed class RuntimeRoutingRule
{
    public string RuleId { get; init; } = string.Empty;

    public string? RoutingIntent { get; init; }

    public string? ModuleId { get; init; }

    public string? Summary { get; init; }

    public RuntimeRoutingRoute PreferredRoute { get; init; } = new();

    public RuntimeRoutingRoute[] FallbackRoutes { get; init; } = [];
}

public sealed class RuntimeRoutingRoute
{
    public string ProviderId { get; init; } = string.Empty;

    public string? BackendId { get; init; }

    public string? RoutingProfileId { get; init; }

    public string? RequestFamily { get; init; }

    public string? BaseUrl { get; init; }

    public string? ApiKeyEnvironmentVariable { get; init; }

    public string? Model { get; init; }
}

public sealed record RuntimeRoutingRuleMatch(
    RuntimeRoutingRule Rule,
    RuntimeRoutingRoute PreferredRoute,
    IReadOnlyList<RuntimeRoutingRoute> FallbackRoutes);
