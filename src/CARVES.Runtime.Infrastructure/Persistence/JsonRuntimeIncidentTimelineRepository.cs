using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRuntimeIncidentTimelineRepository : IRuntimeIncidentTimelineRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRuntimeIncidentTimelineRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<RuntimeIncidentRecord> Load()
    {
        var path = File.Exists(paths.PlatformIncidentTimelineRuntimeFile)
            ? paths.PlatformIncidentTimelineRuntimeFile
            : paths.PlatformIncidentTimelineFile;

        if (!File.Exists(path))
        {
            return Array.Empty<RuntimeIncidentRecord>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<RuntimeIncidentRecord>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<RuntimeIncidentRecord>();
    }

    public void Save(IReadOnlyList<RuntimeIncidentRecord> records)
    {
        using var _ = lockService.Acquire("platform-incident-timeline");
        Directory.CreateDirectory(paths.PlatformEventRuntimeRoot);
        AtomicFileWriter.WriteAllText(
            paths.PlatformIncidentTimelineRuntimeFile,
            JsonSerializer.Serialize(records.OrderByDescending(record => record.OccurredAt).ToArray(), JsonOptions));
    }
}
