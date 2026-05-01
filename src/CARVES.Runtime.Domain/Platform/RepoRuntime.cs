namespace Carves.Runtime.Domain.Platform;

public sealed class RepoRuntime
{
    public int SchemaVersion { get; init; } = 1;

    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string HostId { get; set; } = string.Empty;

    public RepoRuntimeStatus Status { get; set; } = RepoRuntimeStatus.Unknown;

    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch(DateTimeOffset? now = null)
    {
        var value = now ?? DateTimeOffset.UtcNow;
        LastSeen = value;
        UpdatedAt = value;
    }
}
