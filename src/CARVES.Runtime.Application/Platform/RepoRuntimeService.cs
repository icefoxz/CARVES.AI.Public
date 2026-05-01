using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RepoRuntimeService
{
    private readonly IRepoRuntimeRegistryRepository repository;
    private readonly string managingHostId;

    public RepoRuntimeService(IRepoRuntimeRegistryRepository repository, string managingHostId)
    {
        this.repository = repository;
        this.managingHostId = managingHostId;
    }

    public RepoRuntimeRegistry GetRegistry()
    {
        return repository.Load();
    }

    public IReadOnlyList<RepoRuntime> List()
    {
        return repository.Load().Items
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();
    }

    public RepoRuntime Upsert(string repoPath, RepoRuntimeStatus status)
    {
        var normalizedPath = Path.GetFullPath(repoPath);
        var repoId = PlatformIdentity.CreateRepoRuntimeId(normalizedPath);
        var now = DateTimeOffset.UtcNow;
        var registry = repository.Load();
        var existing = registry.Items.FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));
        var runtime = existing is null
            ? new RepoRuntime
            {
                RepoId = repoId,
                RepoPath = normalizedPath,
                HostId = managingHostId,
                Status = status,
                RegisteredAt = now,
                LastSeen = now,
                UpdatedAt = now,
            }
            : new RepoRuntime
            {
                SchemaVersion = existing.SchemaVersion,
                RepoId = existing.RepoId,
                RepoPath = normalizedPath,
                HostId = managingHostId,
                Status = status,
                RegisteredAt = existing.RegisteredAt,
                LastSeen = now,
                UpdatedAt = now,
            };

        var items = registry.Items
            .Where(item => !string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .Append(runtime)
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();

        repository.Save(new RepoRuntimeRegistry
        {
            Items = items,
        });

        return runtime;
    }

    public IReadOnlyList<RepoRuntime> RefreshHostMappings(IEnumerable<string> repoPaths, RepoRuntimeStatus defaultStatus = RepoRuntimeStatus.Idle)
    {
        var normalizedPaths = repoPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var registry = repository.Load();
        var existingByRepoId = registry.Items.ToDictionary(item => item.RepoId, StringComparer.Ordinal);
        var refreshed = new List<RepoRuntime>(normalizedPaths.Length);

        foreach (var normalizedPath in normalizedPaths)
        {
            var repoId = PlatformIdentity.CreateRepoRuntimeId(normalizedPath);
            if (existingByRepoId.TryGetValue(repoId, out var existing))
            {
                refreshed.Add(new RepoRuntime
                {
                    SchemaVersion = existing.SchemaVersion,
                    RepoId = existing.RepoId,
                    RepoPath = normalizedPath,
                    HostId = managingHostId,
                    Status = existing.Status == RepoRuntimeStatus.Unknown ? defaultStatus : existing.Status,
                    RegisteredAt = existing.RegisteredAt,
                    LastSeen = now,
                    UpdatedAt = now,
                });
                continue;
            }

            refreshed.Add(new RepoRuntime
            {
                RepoId = repoId,
                RepoPath = normalizedPath,
                HostId = managingHostId,
                Status = defaultStatus,
                RegisteredAt = now,
                LastSeen = now,
                UpdatedAt = now,
            });
        }

        var items = registry.Items
            .Where(item => !refreshed.Any(refreshedItem => string.Equals(refreshedItem.RepoId, item.RepoId, StringComparison.Ordinal)))
            .Concat(refreshed)
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();
        repository.Save(new RepoRuntimeRegistry
        {
            Items = items,
        });

        return refreshed;
    }

    public static RepoRuntimeStatus FromRuntimeInstanceStatus(RuntimeInstanceStatus status)
    {
        return status switch
        {
            RuntimeInstanceStatus.Running => RepoRuntimeStatus.Active,
            RuntimeInstanceStatus.Attached => RepoRuntimeStatus.Idle,
            RuntimeInstanceStatus.Registered => RepoRuntimeStatus.Idle,
            RuntimeInstanceStatus.Paused => RepoRuntimeStatus.Idle,
            RuntimeInstanceStatus.Stopped => RepoRuntimeStatus.Idle,
            _ => RepoRuntimeStatus.Unknown,
        };
    }
}
