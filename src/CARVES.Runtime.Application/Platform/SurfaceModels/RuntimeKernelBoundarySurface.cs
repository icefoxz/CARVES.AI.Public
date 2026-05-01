using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeKernelBoundarySurface
{
    public string SchemaVersion { get; init; } = "runtime-kernel-boundary.v1";

    public string SurfaceId { get; init; } = string.Empty;

    public string KernelId { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public string[] SuccessCriteria { get; init; } = [];

    public string[] StopConditions { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimeKernelTruthRootDescriptor
{
    public string RootId { get; init; } = string.Empty;

    public string Classification { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] PathRefs { get; init; } = [];

    public string[] TruthRefs { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimeKernelBoundaryRule
{
    public string RuleId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] AllowedRefs { get; init; } = [];

    public string[] ForbiddenRefs { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimeKernelGovernedReadPath
{
    public string PathId { get; init; } = string.Empty;

    public string EntryPoint { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] TruthRefs { get; init; } = [];

    public string[] Notes { get; init; } = [];
}
