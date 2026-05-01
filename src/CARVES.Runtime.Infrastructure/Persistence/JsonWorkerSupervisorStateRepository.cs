using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonWorkerSupervisorStateRepository : IWorkerSupervisorStateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonWorkerSupervisorStateRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public WorkerSupervisorStateSnapshot Load()
    {
        if (!File.Exists(paths.PlatformWorkerSupervisorStateLiveStateFile))
        {
            return new WorkerSupervisorStateSnapshot();
        }

        return JsonSerializer.Deserialize<WorkerSupervisorStateSnapshot>(
            File.ReadAllText(paths.PlatformWorkerSupervisorStateLiveStateFile),
            JsonOptions) ?? new WorkerSupervisorStateSnapshot();
    }

    public void Save(WorkerSupervisorStateSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-worker-supervisor-state");
        Directory.CreateDirectory(paths.PlatformWorkerLiveStateRoot);
        AtomicFileWriter.WriteAllText(
            paths.PlatformWorkerSupervisorStateLiveStateFile,
            JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
