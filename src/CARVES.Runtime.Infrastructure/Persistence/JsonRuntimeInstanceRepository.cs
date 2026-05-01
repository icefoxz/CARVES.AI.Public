using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRuntimeInstanceRepository : IRuntimeInstanceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRuntimeInstanceRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<RuntimeInstance> Load()
    {
        var path = File.Exists(paths.PlatformRuntimeInstancesLiveStateFile)
            ? paths.PlatformRuntimeInstancesLiveStateFile
            : paths.PlatformRuntimeInstancesFile;

        if (!File.Exists(path))
        {
            return Array.Empty<RuntimeInstance>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<RuntimeInstance>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<RuntimeInstance>();
    }

    public void Save(IReadOnlyList<RuntimeInstance> instances)
    {
        using var _ = lockService.Acquire("platform-runtime-instances");
        Directory.CreateDirectory(paths.PlatformSessionLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformRuntimeInstancesLiveStateFile, JsonSerializer.Serialize(instances, JsonOptions));
    }
}
