using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class PlatformSchedulerService
{
    private readonly RepoRegistryService repoRegistryService;
    private readonly RuntimeInstanceManager runtimeInstanceManager;
    private readonly PlatformGovernanceService governanceService;
    private readonly IWorkerNodeRegistryRepository workerNodeRegistryRepository;
    private readonly ProviderRoutingService providerRoutingService;
    private readonly RuntimePolicyBundleService? runtimePolicyBundleService;

    public PlatformSchedulerService(
        RepoRegistryService repoRegistryService,
        RuntimeInstanceManager runtimeInstanceManager,
        PlatformGovernanceService governanceService,
        IWorkerNodeRegistryRepository workerNodeRegistryRepository,
        ProviderRoutingService providerRoutingService,
        RuntimePolicyBundleService? runtimePolicyBundleService = null)
    {
        this.repoRegistryService = repoRegistryService;
        this.runtimeInstanceManager = runtimeInstanceManager;
        this.governanceService = governanceService;
        this.workerNodeRegistryRepository = workerNodeRegistryRepository;
        this.providerRoutingService = providerRoutingService;
        this.runtimePolicyBundleService = runtimePolicyBundleService;
    }

    public PlatformSchedulingDecision Plan(int requestedSlots)
    {
        var schedulerGate = runtimePolicyBundleService is null
            ? null
            : RuntimeRoleModeExecutionGate.EvaluateSchedulerAutoDispatch(runtimePolicyBundleService.LoadRoleGovernancePolicy());
        if (schedulerGate is not null && !schedulerGate.Allowed)
        {
            return new PlatformSchedulingDecision(
                requestedSlots,
                0,
                Array.Empty<string>(),
                Array.Empty<PlatformSchedulingCandidateDecision>(),
                $"{schedulerGate.Summary} outcome={schedulerGate.Outcome}.");
        }

        var platformPolicy = governanceService.GetSnapshot().PlatformPolicy;
        var repoDescriptors = repoRegistryService.List();
        var instances = runtimeInstanceManager.List().ToDictionary(instance => instance.RepoId, StringComparer.Ordinal);
        var availableWorkerSlots = workerNodeRegistryRepository.Load()
            .Where(node => node.Status is not WorkerNodeStatus.Quarantined and not WorkerNodeStatus.Offline)
            .Sum(node => Math.Max(0, node.Capabilities.MaxConcurrentTasks - node.ActiveLeaseCount));
        var grantedSlots = Math.Max(0, Math.Min(requestedSlots, Math.Min(platformPolicy.MaxRepoSelectionsPerTick, availableWorkerSlots)));
        if (grantedSlots == 0)
        {
            return new PlatformSchedulingDecision(requestedSlots, 0, Array.Empty<string>(), Array.Empty<PlatformSchedulingCandidateDecision>(), "No platform capacity is available for repo advancement.");
        }

        var candidates = new List<PlatformSchedulingCandidateDecision>();
        foreach (var descriptor in repoDescriptors)
        {
            if (!instances.TryGetValue(descriptor.RepoId, out var instance))
            {
                continue;
            }

            var repoPolicy = governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
            var route = providerRoutingService.Route(descriptor.RepoId, "worker", allowFallback: true);
            if (!descriptor.RuntimeEnabled)
            {
                candidates.Add(new PlatformSchedulingCandidateDecision(descriptor.RepoId, false, double.MinValue, "Runtime is disabled for this repo."));
                continue;
            }

            if (!route.Allowed)
            {
                candidates.Add(new PlatformSchedulingCandidateDecision(descriptor.RepoId, false, double.MinValue, route.Reason));
                continue;
            }

            if (instance.Status is RuntimeInstanceStatus.Paused or RuntimeInstanceStatus.Stopped)
            {
                candidates.Add(new PlatformSchedulingCandidateDecision(descriptor.RepoId, false, double.MinValue, $"Runtime instance is {instance.Status}."));
                continue;
            }

            var score = ComputeFairnessScore(instance, repoPolicy);
            candidates.Add(new PlatformSchedulingCandidateDecision(descriptor.RepoId, true, score, $"Eligible through repo priority {repoPolicy.RuntimeSelectionPriority} and fairness scoring."));
        }

        var selected = candidates
            .Where(candidate => candidate.Selected)
            .OrderByDescending(candidate => candidate.FairnessScore)
            .ThenBy(candidate => candidate.RepoId, StringComparer.Ordinal)
            .Take(grantedSlots)
            .ToArray();

        foreach (var selectedCandidate in selected)
        {
            var instance = instances[selectedCandidate.RepoId];
            instance.LastPlatformScheduledAt = DateTimeOffset.UtcNow;
            instance.PlatformSelectionCount += 1;
            instance.LastFairnessScore = selectedCandidate.FairnessScore;
            instance.LastSchedulingReason = selectedCandidate.Reason;
            runtimeInstanceManager.Update(instance);
        }

        var candidateResults = candidates
            .Select(candidate => selected.Any(selectedCandidate => string.Equals(selectedCandidate.RepoId, candidate.RepoId, StringComparison.Ordinal))
                ? candidate
                : candidate with { Selected = false, Reason = candidate.Selected ? "Deferred by fairness policy for this scheduling pass." : candidate.Reason })
            .ToArray();
        var reason = selected.Length == 0
            ? "No repo was selected by platform fairness policy."
            : $"Selected {selected.Length} repo runtime(s): {string.Join(", ", selected.Select(candidate => candidate.RepoId))}.";
        return new PlatformSchedulingDecision(requestedSlots, selected.Length, selected.Select(candidate => candidate.RepoId).ToArray(), candidateResults, reason);
    }

    private static double ComputeFairnessScore(RuntimeInstance instance, RepoPolicy repoPolicy)
    {
        var starvationMinutes = instance.LastPlatformScheduledAt is null
            ? repoPolicy.StarvationWindowMinutes
            : (DateTimeOffset.UtcNow - instance.LastPlatformScheduledAt.Value).TotalMinutes;
        return (repoPolicy.RuntimeSelectionPriority * 100d) + starvationMinutes;
    }
}
