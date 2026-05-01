using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRepoRegistryRepository : IRepoRegistryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRepoRegistryRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public RepoRegistry Load()
    {
        if (!File.Exists(paths.PlatformRepoRegistryFile))
        {
            return new RepoRegistry();
        }

        var registry = JsonSerializer.Deserialize<RepoRegistry>(File.ReadAllText(paths.PlatformRepoRegistryFile), JsonOptions) ?? new RepoRegistry();
        var items = registry.Items
            .Select(item => ResolveDescriptor(item.RepoId) ?? item)
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();
        return new RepoRegistry
        {
            Items = items,
        };
    }

    public void Save(RepoRegistry registry)
    {
        using var _ = lockService.Acquire("platform-repo-registry");
        Directory.CreateDirectory(paths.PlatformReposRoot);
        foreach (var item in registry.Items)
        {
            AtomicFileWriter.WriteAllText(Path.Combine(paths.PlatformReposRoot, $"{item.RepoId}.json"), JsonSerializer.Serialize(item, JsonOptions));
        }

        AtomicFileWriter.WriteAllText(paths.PlatformRepoRegistryFile, JsonSerializer.Serialize(new RepoRegistry
        {
            Items = registry.Items.OrderBy(item => item.RepoId, StringComparer.Ordinal).ToArray(),
        }, JsonOptions));
    }

    private RepoDescriptor? ResolveDescriptor(string repoId)
    {
        var path = Path.Combine(paths.PlatformReposRoot, $"{repoId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RepoDescriptor>(File.ReadAllText(path), JsonOptions);
    }
}
