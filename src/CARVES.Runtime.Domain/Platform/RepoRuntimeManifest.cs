namespace Carves.Runtime.Domain.Platform;

public enum RepoRuntimeManifestState
{
    Healthy,
    Dirty,
    Repairing,
}

public sealed class RepoRuntimeManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string GitRoot { get; init; } = string.Empty;

    public string RuntimeRoot { get; init; } = string.Empty;

    public string ActiveBranch { get; set; } = string.Empty;

    public string RuntimeVersion { get; set; } = string.Empty;

    public string ClientVersion { get; set; } = string.Empty;

    public string HostSessionId { get; set; } = string.Empty;

    public string RuntimeStatus { get; set; } = string.Empty;

    public string RepoSummary { get; set; } = string.Empty;

    public RepoRuntimeManifestState State { get; set; } = RepoRuntimeManifestState.Healthy;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastAttachedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastRepairAt { get; set; }
}
