namespace Carves.Runtime.Domain.Platform;

public sealed class HostRegistry
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<HostInstance> Items { get; init; } = Array.Empty<HostInstance>();
}
