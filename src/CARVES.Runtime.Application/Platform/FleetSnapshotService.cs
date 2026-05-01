using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class FleetSnapshotService
{
    private readonly HostRegistryService hostRegistryService;
    private readonly RepoRuntimeService repoRuntimeService;
    private readonly RepoHostMappingService repoHostMappingService;

    public FleetSnapshotService(
        HostRegistryService hostRegistryService,
        RepoRuntimeService repoRuntimeService,
        RepoHostMappingService repoHostMappingService)
    {
        this.hostRegistryService = hostRegistryService;
        this.repoRuntimeService = repoRuntimeService;
        this.repoHostMappingService = repoHostMappingService;
    }

    public FleetSnapshot Build()
    {
        var mappings = repoHostMappingService.List()
            .ToDictionary(item => item.RepoId, StringComparer.Ordinal);

        var repos = repoRuntimeService.List()
            .Select(runtime =>
            {
                var mapping = mappings.TryGetValue(runtime.RepoId, out var value)
                    ? value
                    : null;
                return new FleetRepoSnapshot
                {
                    RepoId = runtime.RepoId,
                    RepoPath = runtime.RepoPath,
                    RepoStatus = runtime.Status,
                    LastSeen = runtime.LastSeen,
                    HostId = runtime.HostId,
                    HostStatus = mapping?.HostStatus,
                    MappingState = mapping?.MappingState ?? RepoHostMappingState.UnknownRepo,
                };
            })
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();

        var hosts = hostRegistryService.List()
            .Select(host => new FleetHostSnapshot
            {
                HostId = host.HostId,
                MachineId = host.MachineId,
                Endpoint = host.Endpoint,
                Status = host.Status,
                LastSeen = host.LastSeen,
            })
            .OrderBy(item => item.HostId, StringComparer.Ordinal)
            .ToArray();

        return new FleetSnapshot
        {
            Hosts = hosts,
            Repos = repos,
        };
    }
}
