namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderPolicy
{
    public string PolicyId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedProviderProfiles { get; init; } = Array.Empty<string>();

    public bool AllowCodeGeneration { get; init; }

    public bool AllowPlanning { get; init; }

    public IReadOnlyList<string> AllowedRepoScopes { get; init; } = Array.Empty<string>();

    public bool AllowFallbackProfiles { get; init; }

    public IReadOnlyList<string> FallbackProviderProfiles { get; init; } = Array.Empty<string>();
}
