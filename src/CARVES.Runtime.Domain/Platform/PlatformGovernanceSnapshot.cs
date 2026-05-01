namespace Carves.Runtime.Domain.Platform;

public sealed class PlatformGovernanceSnapshot
{
    public int Version { get; init; } = 1;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public PlatformPolicy PlatformPolicy { get; init; } = new();

    public IReadOnlyList<RepoPolicy> RepoPolicies { get; init; } = Array.Empty<RepoPolicy>();

    public IReadOnlyList<ProviderPolicy> ProviderPolicies { get; init; } = Array.Empty<ProviderPolicy>();

    public IReadOnlyList<WorkerPolicy> WorkerPolicies { get; init; } = Array.Empty<WorkerPolicy>();

    public IReadOnlyList<ReviewPolicy> ReviewPolicies { get; init; } = Array.Empty<ReviewPolicy>();
}
