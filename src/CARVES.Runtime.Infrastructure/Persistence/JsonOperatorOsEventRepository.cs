using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonOperatorOsEventRepository : IOperatorOsEventRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonOperatorOsEventRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public OperatorOsEventSnapshot Load()
    {
        var path = File.Exists(paths.PlatformOperatorOsEventsRuntimeFile)
            ? paths.PlatformOperatorOsEventsRuntimeFile
            : paths.PlatformOperatorOsEventsFile;

        if (!File.Exists(path))
        {
            return new OperatorOsEventSnapshot();
        }

        return JsonSerializer.Deserialize<OperatorOsEventSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? new OperatorOsEventSnapshot();
    }

    public void Save(OperatorOsEventSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-operator-os-events");
        Directory.CreateDirectory(paths.PlatformEventRuntimeRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformOperatorOsEventsRuntimeFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
