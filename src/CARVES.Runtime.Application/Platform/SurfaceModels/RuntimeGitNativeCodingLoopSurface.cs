using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGitNativeCodingLoopSurface
{
    public string SchemaVersion { get; init; } = "runtime-git-native-coding-loop.v1";

    public string SurfaceId { get; init; } = "runtime-git-native-coding-loop";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public GitNativeCodingLoopPolicy Policy { get; init; } = new();
}

public sealed class GitNativeCodingLoopPolicy
{
    public string SchemaVersion { get; init; } = "git-native-coding-loop.v1";

    public string PolicyId { get; init; } = "git-native-coding-loop";

    public int PolicyVersion { get; init; } = 1;

    public string Summary { get; init; } = string.Empty;

    public GitNativeCodingLoopExtractionBoundary ExtractionBoundary { get; init; } = new();

    public GitNativeRepoMapProjectionBoundary RepoMapProjection { get; init; } = new();

    public GitNativePatchFirstInteractionBoundary PatchFirstInteraction { get; init; } = new();

    public GitNativeEvidenceLoopBoundary EvidenceLoop { get; init; } = new();

    public GitNativeCodingLoopConcernFamily[] ConcernFamilies { get; init; } = [];

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public GitNativeCodingLoopQualificationLine Qualification { get; init; } = new();

    public GitNativeCodingLoopReadinessEntry[] ReadinessMap { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class GitNativeCodingLoopExtractionBoundary
{
    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslatedIntoCarves { get; init; } = [];

    public string[] RejectedAnchors { get; init; } = [];
}

public sealed class GitNativeRepoMapProjectionBoundary
{
    public string Summary { get; init; } = string.Empty;

    public string[] SourceCapabilities { get; init; } = [];

    public string[] CarvesTranslations { get; init; } = [];

    public string[] StrongerTruthDependencies { get; init; } = [];

    public string[] NonGoals { get; init; } = [];
}

public sealed class GitNativePatchFirstInteractionBoundary
{
    public string Summary { get; init; } = string.Empty;

    public string[] InteractionSteps { get; init; } = [];

    public string[] RequiredGates { get; init; } = [];

    public string[] ReviewExpectations { get; init; } = [];

    public string[] RejectedDrift { get; init; } = [];
}

public sealed class GitNativeEvidenceLoopBoundary
{
    public string Summary { get; init; } = string.Empty;

    public string[] EvidenceKinds { get; init; } = [];

    public string[] SupportingUses { get; init; } = [];

    public string[] NonTruthUses { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class GitNativeCodingLoopConcernFamily
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

public sealed class GitNativeCodingLoopQualificationLine
{
    public string Summary { get; init; } = string.Empty;

    public string[] SuccessCriteria { get; init; } = [];

    public string[] RejectedDirections { get; init; } = [];

    public string[] DeferredDirections { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}

public sealed class GitNativeCodingLoopReadinessEntry
{
    public string SemanticId { get; init; } = string.Empty;

    public string Readiness { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] GovernedFollowUp { get; init; } = [];

    public string[] Notes { get; init; } = [];
}
