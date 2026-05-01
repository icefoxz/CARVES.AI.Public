using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeDurableExecutionSemanticsSurface
{
    public string SchemaVersion { get; init; } = "runtime-durable-execution-semantics.v1";

    public string SurfaceId { get; init; } = "runtime-durable-execution-semantics";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public DurableExecutionSemanticsPolicy Policy { get; init; } = new();
}

public sealed class DurableExecutionSemanticsPolicy
{
    public string SchemaVersion { get; init; } = "durable-execution-semantics.v1";

    public string PolicyId { get; init; } = "durable-execution-semantics";

    public int PolicyVersion { get; init; } = 1;

    public string Summary { get; init; } = string.Empty;

    public DurableExecutionExtractionBoundary ExtractionBoundary { get; init; } = new();

    public DurableExecutionConcernFamily[] ConcernFamilies { get; init; } = [];

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public DurableExecutionQualificationLine Qualification { get; init; } = new();

    public DurableExecutionReadinessEntry[] ReadinessMap { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class DurableExecutionExtractionBoundary
{
    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslatedIntoCarves { get; init; } = [];

    public string[] RejectedAnchors { get; init; } = [];
}

public sealed class DurableExecutionConcernFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public string Layer { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string CurrentStatus { get; init; } = string.Empty;

    public string[] SourceProjects { get; init; } = [];

    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslationTargets { get; init; } = [];

    public string[] GovernedEntryPoints { get; init; } = [];

    public string[] ReviewBoundaries { get; init; } = [];

    public string[] OutOfScope { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class DurableExecutionQualificationLine
{
    public string Summary { get; init; } = string.Empty;

    public string[] SuccessCriteria { get; init; } = [];

    public string[] RejectedDirections { get; init; } = [];

    public string[] DeferredDirections { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}

public sealed class DurableExecutionReadinessEntry
{
    public string SemanticId { get; init; } = string.Empty;

    public string Readiness { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] GovernedFollowUp { get; init; } = [];

    public string[] Notes { get; init; } = [];
}
