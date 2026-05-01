using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonWorkerPermissionAuditRepository : IWorkerPermissionAuditRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonWorkerPermissionAuditRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<WorkerPermissionAuditRecord> Load()
    {
        var path = File.Exists(paths.PlatformPermissionAuditRuntimeFile)
            ? paths.PlatformPermissionAuditRuntimeFile
            : paths.PlatformPermissionAuditFile;

        if (!File.Exists(path))
        {
            return Array.Empty<WorkerPermissionAuditRecord>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<WorkerPermissionAuditRecord>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<WorkerPermissionAuditRecord>();
    }

    public void Save(IReadOnlyList<WorkerPermissionAuditRecord> records)
    {
        using var _ = lockService.Acquire("platform-permission-audit");
        Directory.CreateDirectory(paths.PlatformEventRuntimeRoot);
        AtomicFileWriter.WriteAllText(
            paths.PlatformPermissionAuditRuntimeFile,
            JsonSerializer.Serialize(records.OrderByDescending(item => item.OccurredAt).ToArray(), JsonOptions));
    }
}
