using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeMinimalWorkerBaselineSurface
{
    public string SchemaVersion { get; init; } = "runtime-minimal-worker-baseline.v1";

    public string SurfaceId { get; init; } = "runtime-minimal-worker-baseline";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public MinimalWorkerBaselinePolicy Policy { get; init; } = new();
}

public sealed class MinimalWorkerBaselinePolicy
{
    public string SchemaVersion { get; init; } = "minimal-worker-baseline.v1";

    public string PolicyId { get; init; } = "minimal-worker-baseline";

    public int PolicyVersion { get; init; } = 1;

    public string Summary { get; init; } = string.Empty;

    public MinimalWorkerExtractionBoundary ExtractionBoundary { get; init; } = new();

    public MinimalWorkerLinearLoopBaseline LinearLoop { get; init; } = new();

    public MinimalWorkerSubprocessActionBoundary SubprocessBoundary { get; init; } = new();

    public MinimalWorkerWeakLaneBoundary WeakLane { get; init; } = new();

    public MinimalWorkerQualificationLine Qualification { get; init; } = new();

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class MinimalWorkerExtractionBoundary
{
    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslatedIntoCarves { get; init; } = [];

    public string[] RejectedAnchors { get; init; } = [];
}

public sealed class MinimalWorkerLinearLoopBaseline
{
    public string Summary { get; init; } = string.Empty;

    public string[] LoopPhases { get; init; } = [];

    public string[] OrderingCompatibility { get; init; } = [];

    public string[] OutsideWorkerLoop { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class MinimalWorkerSubprocessActionBoundary
{
    public string Summary { get; init; } = string.Empty;

    public string[] Benefits { get; init; } = [];

    public string[] GuardedSeparations { get; init; } = [];

    public string[] RequiredGovernedState { get; init; } = [];

    public string[] OutOfScope { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class MinimalWorkerWeakLaneBoundary
{
    public string LaneId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ExecutionStyle { get; init; } = string.Empty;

    public string ModelProfileId { get; init; } = string.Empty;

    public string[] AllowedTaskTypes { get; init; } = [];

    public string[] RequiredStartupSurfaces { get; init; } = [];

    public string[] RequiredRuntimeSurfaces { get; init; } = [];

    public string[] RequiredVerification { get; init; } = [];

    public AgentGovernanceScopeCeiling ScopeCeiling { get; init; } = new();

    public string[] StopConditions { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class MinimalWorkerQualificationLine
{
    public string Summary { get; init; } = string.Empty;

    public string[] Gains { get; init; } = [];

    public string[] SuccessCriteria { get; init; } = [];

    public string[] IntentionallyOutsideScope { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}
