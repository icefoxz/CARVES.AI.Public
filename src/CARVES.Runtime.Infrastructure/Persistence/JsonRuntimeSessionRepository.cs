using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRuntimeSessionRepository : IRuntimeSessionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRuntimeSessionRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public RuntimeSessionState? Load()
    {
        var sessionPath = ResolveReadablePath();
        if (sessionPath is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimeSessionState>(File.ReadAllText(sessionPath), JsonOptions);
    }

    public void Save(RuntimeSessionState session)
    {
        using var _ = lockService.Acquire("runtime-session");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RuntimeSessionFile)!);
        AtomicFileWriter.WriteAllText(paths.RuntimeSessionFile, JsonSerializer.Serialize(session, JsonOptions));
    }

    public void Delete()
    {
        using var _ = lockService.Acquire("runtime-session");
        if (File.Exists(paths.RuntimeSessionFile))
        {
            File.Delete(paths.RuntimeSessionFile);
        }

        var legacyPath = GetLegacyPath();
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }

    private string? ResolveReadablePath()
    {
        if (File.Exists(paths.RuntimeSessionFile))
        {
            return paths.RuntimeSessionFile;
        }

        var legacyPath = GetLegacyPath();
        return File.Exists(legacyPath)
            ? legacyPath
            : null;
    }

    private string GetLegacyPath()
    {
        return Path.Combine(paths.RuntimeRoot, "session.json");
    }
}
