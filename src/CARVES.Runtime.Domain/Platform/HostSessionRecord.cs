namespace Carves.Runtime.Domain.Platform;

public enum HostSessionStatus
{
    Active,
    Stopped,
}

public sealed record HostSessionRepoBinding
{
    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string ClientRepoRoot { get; init; } = string.Empty;

    public string AttachMode { get; init; } = string.Empty;

    public string RuntimeHealth { get; init; } = string.Empty;

    public DateTimeOffset AttachedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record HostSessionRecord
{
    public int SchemaVersion { get; init; } = 1;

    public string SessionId { get; init; } = string.Empty;

    public string HostId { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string? BaseUrl { get; init; }

    public string Stage { get; init; } = string.Empty;

    public HostSessionStatus Status { get; init; } = HostSessionStatus.Active;

    public HostControlState ControlState { get; init; } = HostControlState.Running;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; init; }

    public string? StopReason { get; init; }

    public HostControlAction LastControlAction { get; init; } = HostControlAction.Started;

    public string? LastControlReason { get; init; }

    public DateTimeOffset? LastControlAt { get; init; }

    public IReadOnlyList<HostSessionRepoBinding> AttachedRepos { get; init; } = Array.Empty<HostSessionRepoBinding>();
}
