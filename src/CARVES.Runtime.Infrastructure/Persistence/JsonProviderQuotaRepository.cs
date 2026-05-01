using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonProviderQuotaRepository : IProviderQuotaRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonProviderQuotaRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ProviderQuotaSnapshot Load()
    {
        var quotaPath = ResolveReadablePath();
        if (quotaPath is null)
        {
            return new ProviderQuotaSnapshot();
        }

        return JsonSerializer.Deserialize<ProviderQuotaSnapshot>(File.ReadAllText(quotaPath), JsonOptions)
            ?? new ProviderQuotaSnapshot();
    }

    public void Save(ProviderQuotaSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-provider-quotas");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.PlatformProviderQuotaFile)!);
        AtomicFileWriter.WriteAllText(paths.PlatformProviderQuotaFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private string? ResolveReadablePath()
    {
        if (File.Exists(paths.PlatformProviderQuotaFile))
        {
            return paths.PlatformProviderQuotaFile;
        }

        var legacyPath = Path.Combine(paths.PlatformProvidersRoot, "quota_usage.json");
        return File.Exists(legacyPath)
            ? legacyPath
            : null;
    }
}
