namespace Carves.Runtime.Application.ControlPlane;

public sealed class ControlPlaneLockLeaseSnapshot
{
    public string Scope { get; init; } = string.Empty;

    public string LeaseId { get; init; } = string.Empty;

    public string LeasePath { get; init; } = string.Empty;

    public string State { get; init; } = "unknown";

    public string Status { get; init; } = "unknown";

    public string? Resource { get; init; }

    public string? Operation { get; init; }

    public string Mode { get; init; } = "write";

    public string OwnerId { get; init; } = string.Empty;

    public int? OwnerProcessId { get; init; }

    public string? OwnerProcessName { get; init; }

    public string? TaskId { get; init; }

    public string? WorkspacePath { get; init; }

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedOperationClasses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedToolsOrAdapters { get; init; } = Array.Empty<string>();

    public string CleanupPosture { get; init; } = ControlPlaneResidueContract.NoCleanupRequiredPosture;

    public DateTimeOffset? AcquiredAt { get; init; }

    public DateTimeOffset? LastHeartbeat { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public TimeSpan? Ttl { get; init; }

    public string Summary { get; init; } = string.Empty;
}
