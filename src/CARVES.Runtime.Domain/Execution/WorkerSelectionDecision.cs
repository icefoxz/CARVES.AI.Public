using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerSelectionDecision
{
    public string RepoId { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public bool Allowed { get; init; }

    public bool UsedFallback { get; init; }

    public string RequestedTrustProfileId { get; init; } = string.Empty;

    public string? SelectedBackendId { get; init; }

    public string? SelectedProviderId { get; init; }

    public string? SelectedAdapterId { get; init; }

    public string? SelectedRoutingProfileId { get; init; }

    public string? ActiveRoutingProfileId { get; init; }

    public string? AppliedRoutingRuleId { get; init; }

    public string? RoutingIntent { get; init; }

    public string? RoutingModuleId { get; init; }

    public string? SelectedModelId { get; init; }

    public string? SelectedRequestFamily { get; init; }

    public string? SelectedBaseUrl { get; init; }

    public string? SelectedApiKeyEnvironmentVariable { get; init; }

    public int? SelectedProviderTimeoutSeconds { get; init; }

    public int? SelectedProviderRetryLimit { get; init; }

    public bool SelectedBackendSupportsLongRunningTasks { get; init; }

    public string RouteSource { get; init; } = "registry";

    public string RouteReason { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public RouteEligibilityStatus? PreferredRouteEligibility { get; init; }

    public string? PreferredIneligibilityReason { get; init; }

    public IReadOnlyList<string> SelectedBecause { get; init; } = Array.Empty<string>();

    public WorkerExecutionProfile? Profile { get; init; }

    public IReadOnlyList<WorkerSelectionCandidate> Candidates { get; init; } = Array.Empty<WorkerSelectionCandidate>();
}

public sealed class WorkerSelectionCandidate
{
    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string? RoutingProfileId { get; init; }

    public string? RoutingRuleId { get; init; }

    public string RouteDisposition { get; init; } = "none";

    public string HealthState { get; init; } = string.Empty;

    public bool ProfileCompatible { get; init; }

    public bool CapabilityCompatible { get; init; }

    public bool Selected { get; init; }

    public RouteEligibilityStatus Eligibility { get; init; } = RouteEligibilityStatus.Unsupported;

    public RouteSelectionSignals Signals { get; init; } = new();

    public string Reason { get; init; } = string.Empty;
}
