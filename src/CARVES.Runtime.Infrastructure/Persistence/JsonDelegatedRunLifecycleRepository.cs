using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonDelegatedRunLifecycleRepository : IDelegatedRunLifecycleRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonDelegatedRunLifecycleRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public DelegatedRunLifecycleSnapshot Load()
    {
        var path = File.Exists(paths.PlatformDelegatedRunLifecycleLiveStateFile)
            ? paths.PlatformDelegatedRunLifecycleLiveStateFile
            : paths.PlatformDelegatedRunLifecycleFile;

        if (!File.Exists(path))
        {
            return new DelegatedRunLifecycleSnapshot();
        }

        return JsonSerializer.Deserialize<DelegatedRunLifecycleSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? new DelegatedRunLifecycleSnapshot();
    }

    public void Save(DelegatedRunLifecycleSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-delegated-run-lifecycles");
        Directory.CreateDirectory(paths.PlatformDelegationLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformDelegatedRunLifecycleLiveStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
