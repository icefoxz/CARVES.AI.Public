using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeCodeUnderstandingEngineService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeCodeUnderstandingEngineSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeCodeUnderstandingEngineSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = ToRepoRelative(paths.PlatformCodeUnderstandingEngineFile),
            Policy = policy,
        };
    }

    public CodeUnderstandingEnginePolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }
}
