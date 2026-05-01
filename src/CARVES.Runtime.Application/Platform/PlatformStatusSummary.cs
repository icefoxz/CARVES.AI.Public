namespace Carves.Runtime.Application.Platform;

public sealed record PlatformStatusSummary(
    int RegisteredRepoCount,
    int RuntimeInstanceCount,
    int RunningInstanceCount,
    int OpenOpportunityCount,
    int ActiveSessionCount,
    int ProviderCount,
    int WorkerNodeCount,
    int ActiveLeaseCount,
    int StaleProjectionCount,
    IReadOnlyList<RepoRuntimeSummaryDto> Repos);
