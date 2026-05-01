namespace Carves.Runtime.Domain.Platform;

public enum RepoRuntimeHealthState
{
    Healthy,
    Dirty,
    Broken,
}

public sealed record RepoRuntimeHealthIssue(
    string Code,
    string Summary,
    string Severity,
    string? Path = null);

public sealed class RepoRuntimeHealthCheckResult
{
    public RepoRuntimeHealthState State { get; init; } = RepoRuntimeHealthState.Healthy;

    public IReadOnlyList<RepoRuntimeHealthIssue> Issues { get; init; } = Array.Empty<RepoRuntimeHealthIssue>();

    public IReadOnlyList<string> MissingDirectories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> InterruptedTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DanglingArtifactPaths { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = "Runtime health has not been evaluated.";

    public string SuggestedAction { get; init; } = "observe";
}
