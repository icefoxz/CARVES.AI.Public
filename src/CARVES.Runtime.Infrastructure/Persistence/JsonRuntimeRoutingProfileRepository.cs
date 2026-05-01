using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRuntimeRoutingProfileRepository : IRuntimeRoutingProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRuntimeRoutingProfileRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public RuntimeRoutingProfile? LoadActive()
    {
        if (!File.Exists(paths.PlatformActiveRoutingProfileFile))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimeRoutingProfile>(File.ReadAllText(paths.PlatformActiveRoutingProfileFile), JsonOptions);
    }

    public void SaveActive(RuntimeRoutingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using var _ = lockService.Acquire("platform-active-routing-profile");
        Directory.CreateDirectory(paths.PlatformRuntimeStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformActiveRoutingProfileFile, JsonSerializer.Serialize(profile, JsonOptions));
    }
}
