using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeWeakModelExecutionLaneService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly CurrentModelQualificationService currentModelQualificationService;

    public RuntimeWeakModelExecutionLaneService(
        string repoRoot,
        ControlPlanePaths paths,
        CurrentModelQualificationService currentModelQualificationService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.currentModelQualificationService = currentModelQualificationService;
    }

    public RuntimeWeakModelExecutionLaneSurface Build()
    {
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var routing = new RuntimeAgentModelProfileRoutingService(repoRoot, paths, currentModelQualificationService).Build().Routing;
        var matched = routing.AvailableLanes
            .Where(item => string.Equals(item.MatchedProfileId, "weak", StringComparison.Ordinal))
            .ToArray();
        var summary = matched.Length == 0
            ? "Weak-model bounded execution lane is defined in policy truth; no currently qualified runtime lanes map directly to the weak profile."
            : $"Weak-model bounded execution lane is active for {matched.Length} qualified lane(s).";

        return new RuntimeWeakModelExecutionLaneSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            LaneSnapshot = new AgentWeakModelExecutionLaneSnapshot
            {
                Summary = summary,
                Lanes = policy.WeakExecutionLanes,
                MatchedQualifiedLanes = matched,
            },
        };
    }
}
