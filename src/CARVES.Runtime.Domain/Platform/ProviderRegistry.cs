namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderRegistry
{
    public int Version { get; init; } = 1;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ProviderDescriptor> Items { get; init; } = Array.Empty<ProviderDescriptor>();
}
