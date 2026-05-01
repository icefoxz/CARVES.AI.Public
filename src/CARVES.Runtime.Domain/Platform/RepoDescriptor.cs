namespace Carves.Runtime.Domain.Platform;

public sealed class RepoDescriptor
{
    public int SchemaVersion { get; init; } = 1;

    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public bool RuntimeEnabled { get; set; } = true;

    public string ProviderProfile { get; set; } = "default";

    public string PolicyProfile { get; set; } = "balanced";

    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
