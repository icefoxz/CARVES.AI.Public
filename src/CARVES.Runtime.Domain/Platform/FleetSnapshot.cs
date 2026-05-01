namespace Carves.Runtime.Domain.Platform;

public sealed class FleetSnapshot
{
    public IReadOnlyList<FleetHostSnapshot> Hosts { get; init; } = [];

    public IReadOnlyList<FleetRepoSnapshot> Repos { get; init; } = [];
}

public sealed class FleetHostSnapshot
{
    public string HostId { get; init; } = string.Empty;

    public string MachineId { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public HostInstanceStatus Status { get; init; } = HostInstanceStatus.Unknown;

    public DateTimeOffset LastSeen { get; init; }
}

public sealed class FleetRepoSnapshot
{
    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public RepoRuntimeStatus RepoStatus { get; init; } = RepoRuntimeStatus.Unknown;

    public DateTimeOffset LastSeen { get; init; }

    public string HostId { get; init; } = string.Empty;

    public HostInstanceStatus? HostStatus { get; init; }

    public RepoHostMappingState MappingState { get; init; } = RepoHostMappingState.UnknownRepo;
}
