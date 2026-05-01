namespace Carves.Runtime.Application.ControlPlane;

public sealed class ControlPlaneLockOptions
{
    public string? Resource { get; init; }

    public string? Operation { get; init; }

    public string Mode { get; init; } = "write";

    public string? TaskId { get; init; }

    public string? WorkspacePath { get; init; }

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedOperationClasses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedToolsOrAdapters { get; init; } = Array.Empty<string>();

    public string CleanupPosture { get; init; } = ControlPlaneResidueContract.NoCleanupRequiredPosture;

    public TimeSpan LeaseTtl { get; init; } = TimeSpan.FromMinutes(2);
}
