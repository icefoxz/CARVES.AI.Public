namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeConsistencyFinding
{
    public string Category { get; init; } = string.Empty;

    public RuntimeConsistencySeverity Severity { get; init; } = RuntimeConsistencySeverity.Warning;

    public string Summary { get; init; } = string.Empty;

    public string LikelyCause { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? LeaseId { get; init; }

    public string? RunId { get; init; }

    public string? PermissionRequestId { get; init; }

    public string? RepoTruthAnchor { get; init; }

    public string? PlatformTruthAnchor { get; init; }
}
