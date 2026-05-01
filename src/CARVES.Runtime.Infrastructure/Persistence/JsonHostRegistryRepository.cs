using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonHostRegistryRepository : IHostRegistryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonHostRegistryRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public HostRegistry Load()
    {
        var path = File.Exists(paths.PlatformHostRegistryLiveStateFile)
            ? paths.PlatformHostRegistryLiveStateFile
            : paths.PlatformHostRegistryFile;

        if (!File.Exists(path))
        {
            return new HostRegistry();
        }

        return JsonSerializer.Deserialize<HostRegistry>(File.ReadAllText(path), JsonOptions) ?? new HostRegistry();
    }

    public void Save(HostRegistry registry)
    {
        using var _ = lockService.Acquire("platform-host-registry");
        Directory.CreateDirectory(paths.PlatformFleetLiveStateRoot);
        var normalized = new HostRegistry
        {
            SchemaVersion = registry.SchemaVersion,
            Items = registry.Items
                .OrderBy(item => item.HostId, StringComparer.Ordinal)
                .ToArray(),
        };
        AtomicFileWriter.WriteAllText(paths.PlatformHostRegistryLiveStateFile, JsonSerializer.Serialize(normalized, JsonOptions));
    }
}
