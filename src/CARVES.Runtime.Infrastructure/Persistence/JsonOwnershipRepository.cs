using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonOwnershipRepository : IOwnershipRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonOwnershipRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public OwnershipSnapshot Load()
    {
        var path = File.Exists(paths.PlatformOwnershipLiveStateFile)
            ? paths.PlatformOwnershipLiveStateFile
            : paths.PlatformOwnershipFile;

        if (!File.Exists(path))
        {
            return new OwnershipSnapshot();
        }

        return JsonSerializer.Deserialize<OwnershipSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? new OwnershipSnapshot();
    }

    public void Save(OwnershipSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-ownership");
        Directory.CreateDirectory(paths.PlatformSessionLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformOwnershipLiveStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
