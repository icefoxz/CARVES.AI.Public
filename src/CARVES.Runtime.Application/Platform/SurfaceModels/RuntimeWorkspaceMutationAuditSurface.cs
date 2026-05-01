namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeWorkspaceMutationAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-workspace-mutation-audit.v1";

    public string SurfaceId { get; init; } = "runtime-workspace-mutation-audit";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string ResultReturnChannel { get; init; } = string.Empty;

    public string Status { get; init; } = "not_evaluated";

    public bool LeaseAware { get; init; }

    public string? LeaseId { get; init; }

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = [];

    public int ChangedPathCount { get; init; }

    public int ViolationCount { get; init; }

    public int ScopeEscapeCount { get; init; }

    public int HostOnlyCount { get; init; }

    public int DenyCount { get; init; }

    public bool CanProceedToWriteback { get; init; } = true;

    public IReadOnlyList<RuntimeWorkspaceMutationTouchedPathSurface> ChangedPaths { get; init; } = [];

    public IReadOnlyList<RuntimeWorkspaceMutationAuditBlockerSurface> Blockers { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;
}

public sealed class RuntimeWorkspaceMutationTouchedPathSurface
{
    public string Path { get; init; } = string.Empty;

    public string PolicyClass { get; init; } = string.Empty;

    public string AssetClass { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeWorkspaceMutationAuditBlockerSurface
{
    public string BlockerId { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string PolicyClass { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;
}
