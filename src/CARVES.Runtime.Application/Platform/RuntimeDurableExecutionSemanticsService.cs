using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeDurableExecutionSemanticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeDurableExecutionSemanticsService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeDurableExecutionSemanticsSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeDurableExecutionSemanticsSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformDurableExecutionSemanticsFile),
            Policy = policy,
        };
    }

    public DurableExecutionSemanticsPolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }

    private DurableExecutionSemanticsPolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformDurableExecutionSemanticsFile))
        {
            var persisted = JsonSerializer.Deserialize<DurableExecutionSemanticsPolicy>(
                                File.ReadAllText(paths.PlatformDurableExecutionSemanticsFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformDurableExecutionSemanticsFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformDurableExecutionSemanticsFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private static bool NeedsRefresh(DurableExecutionSemanticsPolicy policy)
    {
        return policy.PolicyVersion < 1
               || policy.ConcernFamilies.Length < 5
               || policy.ReadinessMap.Length < 6
               || policy.BoundaryRules.Length < 5
               || policy.GovernedReadPaths.Length < 5
               || !policy.ExtractionBoundary.RejectedAnchors.Contains(
                   "replacing CARVES TaskGraph with LangGraph as the direct execution substrate",
                   StringComparer.Ordinal)
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "taskgraph_replacement", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal));
    }
}
