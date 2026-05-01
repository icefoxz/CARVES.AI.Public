namespace Carves.Runtime.Domain.Execution;

public sealed record ManagedWorkspacePathPolicyAssessment
{
    public bool EnforcementActive { get; init; }

    public bool LeaseAware { get; init; }

    public string Status { get; init; } = "not_required";

    public string Summary { get; init; } = "Managed workspace path policy enforcement did not apply.";

    public string RecommendedNextAction { get; init; } = "observe current state";

    public string? LeaseId { get; init; }

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ManagedWorkspaceTouchedPath> TouchedPaths { get; init; } = Array.Empty<ManagedWorkspaceTouchedPath>();

    public int WorkspaceOpenCount { get; init; }

    public int ReviewRequiredCount { get; init; }

    public int ScopeEscapeCount { get; init; }

    public int HostOnlyCount { get; init; }

    public int DenyCount { get; init; }

    public bool BlocksExecutionStop => ScopeEscapeCount > 0 || HostOnlyCount > 0 || DenyCount > 0;
}

public sealed record ManagedWorkspaceTouchedPath
{
    public string Path { get; init; } = string.Empty;

    public string PolicyClass { get; init; } = string.Empty;

    public string AssetClass { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}
