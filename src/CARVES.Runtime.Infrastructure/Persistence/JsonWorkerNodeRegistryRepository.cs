using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonWorkerNodeRegistryRepository : IWorkerNodeRegistryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonWorkerNodeRegistryRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<WorkerNode> Load()
    {
        var path = File.Exists(paths.PlatformWorkerRegistryLiveStateFile)
            ? paths.PlatformWorkerRegistryLiveStateFile
            : paths.PlatformWorkerRegistryFile;

        if (!File.Exists(path))
        {
            return Array.Empty<WorkerNode>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<WorkerNode>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<WorkerNode>();
    }

    public void Save(IReadOnlyList<WorkerNode> nodes)
    {
        using var _ = lockService.Acquire("platform-worker-nodes");
        Directory.CreateDirectory(paths.PlatformWorkerLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformWorkerRegistryLiveStateFile, JsonSerializer.Serialize(nodes.OrderBy(item => item.NodeId, StringComparer.Ordinal).ToArray(), JsonOptions));
    }
}
