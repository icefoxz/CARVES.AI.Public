namespace Carves.Runtime.Domain.Platform;

public sealed class RepoRuntimeRegistry
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<RepoRuntime> Items { get; init; } = Array.Empty<RepoRuntime>();
}
