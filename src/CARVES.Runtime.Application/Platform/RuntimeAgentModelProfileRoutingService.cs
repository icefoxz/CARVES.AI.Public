using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentModelProfileRoutingService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly CurrentModelQualificationService currentModelQualificationService;

    public RuntimeAgentModelProfileRoutingService(
        string repoRoot,
        ControlPlanePaths paths,
        CurrentModelQualificationService currentModelQualificationService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.currentModelQualificationService = currentModelQualificationService;
    }

    public RuntimeAgentModelProfileRoutingSurface Build()
    {
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var matrix = currentModelQualificationService.LoadOrCreateMatrix();
        var availableLanes = matrix.Lanes
            .OrderBy(item => item.LaneId, StringComparer.Ordinal)
            .Select(item =>
            {
                var match = ResolveProfileMatch(policy.ModelProfiles, item);
                return new AgentModelProfileLaneMatch
                {
                    LaneId = item.LaneId,
                    ProviderId = item.ProviderId,
                    BackendId = item.BackendId,
                    Model = item.Model,
                    MatchedProfileId = match.ProfileId,
                    Reason = match.Reason,
                };
            })
            .ToArray();

        var summary = availableLanes.Length == 0
            ? "No currently connected qualification lanes are available; model profile routing remains policy-only."
            : $"Projected {availableLanes.Length} currently connected qualification lane(s) across {policy.ModelProfiles.Length} governance profile(s).";

        return new RuntimeAgentModelProfileRoutingSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Routing = new AgentModelProfileRoutingSnapshot
            {
                Summary = summary,
                Profiles = policy.ModelProfiles,
                AvailableLanes = availableLanes,
            },
        };
    }

    private static (string ProfileId, string Reason) ResolveProfileMatch(
        IReadOnlyList<AgentGovernanceModelProfile> profiles,
        ModelQualificationLane lane)
    {
        foreach (var profile in profiles)
        {
            if (profile.PreferredBackendIds.Contains(lane.BackendId, StringComparer.OrdinalIgnoreCase))
            {
                return (profile.ProfileId, $"Matched backend_id={lane.BackendId} against profile preferred backends.");
            }
        }

        var fallback = profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, "standard", StringComparison.Ordinal))
                       ?? profiles.First();
        return (fallback.ProfileId, $"No explicit backend match for {lane.BackendId}; defaulted to profile {fallback.ProfileId}.");
    }
}
