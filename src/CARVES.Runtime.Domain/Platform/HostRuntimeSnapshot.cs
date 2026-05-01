namespace Carves.Runtime.Domain.Platform;

public sealed class HostRuntimeSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public string RepoRoot { get; init; } = string.Empty;

    public HostRuntimeSnapshotState State { get; init; } = HostRuntimeSnapshotState.None;

    public string Summary { get; init; } = string.Empty;

    public string? BaseUrl { get; init; }

    public int? Port { get; init; }

    public int? ProcessId { get; init; }

    public string? RuntimeDirectory { get; init; }

    public string? DeploymentDirectory { get; init; }

    public string? ExecutablePath { get; init; }

    public string Version { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;

    public string? SessionStatus { get; init; }

    public string? HostControlState { get; init; }

    public string? HostControlReason { get; init; }

    public int ActiveWorkerCount { get; init; }

    public IReadOnlyList<string> ActiveTaskIds { get; init; } = Array.Empty<string>();

    public int PendingApprovalCount { get; init; }

    public bool Rehydrated { get; init; }

    public string RehydrationSummary { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastRequestAt { get; init; }

    public DateTimeOffset? LastLoopAt { get; init; }

    public string? LastLoopReason { get; init; }

    public int RequestCount { get; init; }
}
