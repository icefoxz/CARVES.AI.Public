using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonWorkerLeaseRepository : IWorkerLeaseRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonWorkerLeaseRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<WorkerLeaseRecord> Load()
    {
        var path = File.Exists(paths.PlatformWorkerLeasesLiveStateFile)
            ? paths.PlatformWorkerLeasesLiveStateFile
            : paths.PlatformWorkerLeasesFile;

        if (!File.Exists(path))
        {
            return Array.Empty<WorkerLeaseRecord>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<WorkerLeaseRecord>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<WorkerLeaseRecord>();
    }

    public void Save(IReadOnlyList<WorkerLeaseRecord> leases)
    {
        using var _ = lockService.Acquire("platform-worker-leases");
        Directory.CreateDirectory(paths.PlatformWorkerLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformWorkerLeasesLiveStateFile, JsonSerializer.Serialize(leases.OrderBy(item => item.LeaseId, StringComparer.Ordinal).ToArray(), JsonOptions));
    }
}
