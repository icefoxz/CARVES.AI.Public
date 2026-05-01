namespace Carves.Runtime.Domain.Platform;

public sealed class RepoRegistry
{
    public int Version { get; init; } = 1;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<RepoDescriptor> Items { get; init; } = Array.Empty<RepoDescriptor>();
}
