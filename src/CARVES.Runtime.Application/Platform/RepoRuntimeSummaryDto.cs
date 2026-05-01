namespace Carves.Runtime.Application.Platform;

public sealed record RepoRuntimeSummaryDto(
    string RepoId,
    string RepoPath,
    string Stage,
    string RuntimeStatus,
    int OpenTasks,
    int ReviewTasks,
    int OpenOpportunities,
    string Actionability,
    string ProviderProfile,
    string PolicyProfile,
    string TruthSource,
    string ProjectionFreshness,
    string ProjectionOutcome,
    string GatewayMode,
    string GatewayHealth,
    string? GatewayReason,
    string? LastSchedulingReason);
