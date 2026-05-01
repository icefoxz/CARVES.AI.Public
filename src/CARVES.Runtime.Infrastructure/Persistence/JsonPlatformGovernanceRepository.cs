using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonPlatformGovernanceRepository : IPlatformGovernanceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonPlatformGovernanceRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public PlatformGovernanceSnapshot Load()
    {
        if (!File.Exists(paths.PlatformGovernanceFile))
        {
            return new PlatformGovernanceSnapshot();
        }

        return JsonSerializer.Deserialize<PlatformGovernanceSnapshot>(File.ReadAllText(paths.PlatformGovernanceFile), JsonOptions)
            ?? new PlatformGovernanceSnapshot();
    }

    public IReadOnlyList<GovernanceEvent> LoadEvents()
    {
        var path = File.Exists(paths.PlatformGovernanceEventsRuntimeFile)
            ? paths.PlatformGovernanceEventsRuntimeFile
            : paths.PlatformGovernanceEventsFile;

        if (!File.Exists(path))
        {
            return Array.Empty<GovernanceEvent>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<GovernanceEvent>>(File.ReadAllText(path), JsonOptions)
            ?? Array.Empty<GovernanceEvent>();
    }

    public void Save(PlatformGovernanceSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-governance");
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        AtomicFileWriter.WriteAllTextIfChanged(paths.PlatformGovernanceFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    public void SaveEvents(IReadOnlyList<GovernanceEvent> events)
    {
        using var _ = lockService.Acquire("platform-governance-events");
        Directory.CreateDirectory(paths.PlatformEventRuntimeRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformGovernanceEventsRuntimeFile, JsonSerializer.Serialize(events.OrderByDescending(item => item.OccurredAt).ToArray(), JsonOptions));
    }
}
