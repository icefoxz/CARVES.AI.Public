using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonWorktreeRuntimeRepository : IWorktreeRuntimeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonWorktreeRuntimeRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public WorktreeRuntimeSnapshot Load()
    {
        var worktreeStatePath = ResolveReadablePath();
        if (worktreeStatePath is null)
        {
            return new WorktreeRuntimeSnapshot();
        }

        return JsonSerializer.Deserialize<WorktreeRuntimeSnapshot>(File.ReadAllText(worktreeStatePath), JsonOptions)
            ?? new WorktreeRuntimeSnapshot();
    }

    public void Save(WorktreeRuntimeSnapshot snapshot)
    {
        using var _ = lockService.Acquire("runtime-worktrees");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RuntimeWorktreeStateFile)!);
        AtomicFileWriter.WriteAllText(paths.RuntimeWorktreeStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private string? ResolveReadablePath()
    {
        if (File.Exists(paths.RuntimeWorktreeStateFile))
        {
            return paths.RuntimeWorktreeStateFile;
        }

        var legacyPath = GetLegacyPath();
        return File.Exists(legacyPath)
            ? legacyPath
            : null;
    }

    private string GetLegacyPath()
    {
        return Path.Combine(paths.RuntimeRoot, "worktrees.json");
    }
}
