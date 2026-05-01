namespace Carves.Runtime.Domain.Platform;

public sealed class RepoHostMapping
{
    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string HostId { get; init; } = string.Empty;

    public RepoRuntimeStatus RepoStatus { get; init; } = RepoRuntimeStatus.Unknown;

    public HostInstanceStatus? HostStatus { get; init; }

    public RepoHostMappingState MappingState { get; init; } = RepoHostMappingState.UnknownRepo;
}
