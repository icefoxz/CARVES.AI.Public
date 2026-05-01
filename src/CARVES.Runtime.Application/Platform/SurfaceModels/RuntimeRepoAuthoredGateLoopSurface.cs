using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeRepoAuthoredGateLoopSurface
{
    public string SchemaVersion { get; init; } = "runtime-repo-authored-gate-loop.v1";

    public string SurfaceId { get; init; } = "runtime-repo-authored-gate-loop";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public RepoAuthoredGateLoopPolicy Policy { get; init; } = new();
}

public sealed class RepoAuthoredGateLoopPolicy
{
    public string SchemaVersion { get; init; } = "repo-authored-gate-loop.v1";

    public string PolicyId { get; init; } = "repo-authored-gate-loop";

    public int PolicyVersion { get; init; } = 1;

    public string Summary { get; init; } = string.Empty;

    public RepoAuthoredGateLoopExtractionBoundary ExtractionBoundary { get; init; } = new();

    public RepoAuthoredReviewDefinitionKernel DefinitionKernel { get; init; } = new();

    public RepoAuthoredGateExecutionSurface GateExecution { get; init; } = new();

    public RepoAuthoredWorkflowProjection[] WorkflowProjections { get; init; } = [];

    public RepoAuthoredGateConcernFamily[] ConcernFamilies { get; init; } = [];

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public RepoAuthoredGateQualificationLine Qualification { get; init; } = new();

    public RepoAuthoredGateReadinessEntry[] ReadinessMap { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RepoAuthoredGateLoopExtractionBoundary
{
    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslatedIntoCarves { get; init; } = [];

    public string[] RejectedAnchors { get; init; } = [];
}

public sealed class RepoAuthoredReviewDefinitionKernel
{
    public string Summary { get; init; } = string.Empty;

    public string MachineTruthPath { get; init; } = string.Empty;

    public string[] ProjectionPaths { get; init; } = [];

    public string[] DiscoveryModes { get; init; } = [];

    public string[] UnitFamilies { get; init; } = [];

    public string[] NonGoals { get; init; } = [];
}

public sealed class RepoAuthoredGateExecutionSurface
{
    public string Summary { get; init; } = string.Empty;

    public string[] ExecutionUnitKinds { get; init; } = [];

    public string[] ResultStates { get; init; } = [];

    public string[] ArtifactFamilies { get; init; } = [];

    public string[] ReviewBoundaries { get; init; } = [];

    public string[] NonGoals { get; init; } = [];
}

public sealed class RepoAuthoredWorkflowProjection
{
    public string ProjectionId { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] TriggerSurfaces { get; init; } = [];

    public string[] ResultSurfaces { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RepoAuthoredGateConcernFamily
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

public sealed class RepoAuthoredGateQualificationLine
{
    public string Summary { get; init; } = string.Empty;

    public string[] SuccessCriteria { get; init; } = [];

    public string[] RejectedDirections { get; init; } = [];

    public string[] DeferredDirections { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}

public sealed class RepoAuthoredGateReadinessEntry
{
    public string SemanticId { get; init; } = string.Empty;

    public string Readiness { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] GovernedFollowUp { get; init; } = [];

    public string[] Notes { get; init; } = [];
}
