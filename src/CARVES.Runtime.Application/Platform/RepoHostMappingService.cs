using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RepoHostMappingService
{
    private readonly RepoRegistryService repoRegistryService;
    private readonly RepoRuntimeService repoRuntimeService;
    private readonly HostRegistryService hostRegistryService;

    public RepoHostMappingService(
        RepoRegistryService repoRegistryService,
        RepoRuntimeService repoRuntimeService,
        HostRegistryService hostRegistryService)
    {
        this.repoRegistryService = repoRegistryService;
        this.repoRuntimeService = repoRuntimeService;
        this.hostRegistryService = hostRegistryService;
    }

    public IReadOnlyList<RepoRuntime> RefreshManagedMappings()
    {
        return repoRuntimeService.RefreshHostMappings(repoRegistryService.List().Select(item => item.RepoPath));
    }

    public IReadOnlyList<RepoHostMapping> List()
    {
        var knownRepoPaths = repoRegistryService.List()
            .Select(item => NormalizePath(item.RepoPath))
            .ToHashSet(StringComparer.Ordinal);
        var hosts = hostRegistryService.List()
            .ToDictionary(item => item.HostId, StringComparer.Ordinal);

        return repoRuntimeService.List()
            .Select(runtime =>
            {
                var normalizedPath = NormalizePath(runtime.RepoPath);
                HostInstance? host = null;
                var hostKnown = !string.IsNullOrWhiteSpace(runtime.HostId) && hosts.TryGetValue(runtime.HostId, out host);
                return new RepoHostMapping
                {
                    RepoId = runtime.RepoId,
                    RepoPath = runtime.RepoPath,
                    HostId = runtime.HostId,
                    RepoStatus = runtime.Status,
                    HostStatus = hostKnown ? host!.Status : null,
                    MappingState = ResolveState(normalizedPath, runtime.HostId, hostKnown ? host : null, knownRepoPaths),
                };
            })
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();
    }

    private static RepoHostMappingState ResolveState(
        string normalizedRepoPath,
        string hostId,
        HostInstance? host,
        IReadOnlySet<string> knownRepoPaths)
    {
        if (!knownRepoPaths.Contains(normalizedRepoPath))
        {
            return RepoHostMappingState.UnknownRepo;
        }

        if (string.IsNullOrWhiteSpace(hostId) || host is null || host.Status == HostInstanceStatus.Unknown)
        {
            return RepoHostMappingState.Orphaned;
        }

        return RepoHostMappingState.Mapped;
    }

    private static string NormalizePath(string repoPath)
    {
        return Path.GetFullPath(repoPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
