using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRepoRuntimeRegistryRepository : IRepoRuntimeRegistryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRepoRuntimeRegistryRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public RepoRuntimeRegistry Load()
    {
        var path = File.Exists(paths.PlatformRepoRuntimeRegistryLiveStateFile)
            ? paths.PlatformRepoRuntimeRegistryLiveStateFile
            : paths.PlatformRepoRuntimeRegistryFile;

        if (!File.Exists(path))
        {
            return new RepoRuntimeRegistry();
        }

        var registry = JsonSerializer.Deserialize<RepoRuntimeRegistry>(
            File.ReadAllText(path),
            JsonOptions) ?? new RepoRuntimeRegistry();

        return new RepoRuntimeRegistry
        {
            Items = registry.Items
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public void Save(RepoRuntimeRegistry registry)
    {
        using var _ = lockService.Acquire("platform-fleet-repos");
        Directory.CreateDirectory(paths.PlatformFleetLiveStateRoot);
        var ordered = new RepoRuntimeRegistry
        {
            Items = registry.Items
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ToArray(),
        };
        AtomicFileWriter.WriteAllText(
            paths.PlatformRepoRuntimeRegistryLiveStateFile,
            JsonSerializer.Serialize(ordered, JsonOptions));
    }
}
