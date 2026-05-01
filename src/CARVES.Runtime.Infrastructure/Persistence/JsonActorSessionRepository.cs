using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonActorSessionRepository : IActorSessionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonActorSessionRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ActorSessionSnapshot Load()
    {
        var path = File.Exists(paths.PlatformActorSessionsLiveStateFile)
            ? paths.PlatformActorSessionsLiveStateFile
            : paths.PlatformActorSessionsFile;

        if (!File.Exists(path))
        {
            return new ActorSessionSnapshot();
        }

        return JsonSerializer.Deserialize<ActorSessionSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? new ActorSessionSnapshot();
    }

    public void Save(ActorSessionSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-actor-sessions");
        Directory.CreateDirectory(paths.PlatformSessionLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformActorSessionsLiveStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
