namespace Carves.Runtime.Domain.Platform;

public sealed class RuntimeInstance
{
    public int SchemaVersion { get; init; } = 1;

    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public RuntimeInstanceStatus Status { get; set; } = RuntimeInstanceStatus.Registered;

    public string? ActiveSessionId { get; set; }

    public string ProviderBindingId { get; set; } = "default";

    public string PolicyBindingId { get; set; } = "balanced";

    public RepoRuntimeProjection Projection { get; set; } = new();

    public RepoRuntimeGatewayMode GatewayMode { get; set; } = RepoRuntimeGatewayMode.Local;

    public RepoRuntimeGatewayHealthState GatewayHealth { get; set; } = RepoRuntimeGatewayHealthState.Healthy;

    public string? GatewayReason { get; set; }

    public DateTimeOffset? LastPlatformScheduledAt { get; set; }

    public int PlatformSelectionCount { get; set; }

    public double LastFairnessScore { get; set; }

    public string? LastSchedulingReason { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
