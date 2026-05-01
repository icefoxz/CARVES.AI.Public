using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonProviderHealthRepository : IProviderHealthRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonProviderHealthRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ProviderHealthSnapshot Load()
    {
        var providerHealthPath = ResolveReadablePath();
        if (providerHealthPath is null)
        {
            return new ProviderHealthSnapshot();
        }

        return JsonSerializer.Deserialize<ProviderHealthSnapshot>(File.ReadAllText(providerHealthPath), JsonOptions)
            ?? new ProviderHealthSnapshot();
    }

    public void Save(ProviderHealthSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-provider-health");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.PlatformProviderHealthFile)!);
        AtomicFileWriter.WriteAllText(paths.PlatformProviderHealthFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private string? ResolveReadablePath()
    {
        if (File.Exists(paths.PlatformProviderHealthFile))
        {
            return paths.PlatformProviderHealthFile;
        }

        var legacyPath = Path.Combine(paths.PlatformProvidersRoot, "health.json");
        return File.Exists(legacyPath)
            ? legacyPath
            : null;
    }
}
