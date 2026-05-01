using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeCodeUnderstandingEngineSurface
{
    public string SchemaVersion { get; init; } = "runtime-code-understanding-engine.v2";

    public string SurfaceId { get; init; } = "runtime-code-understanding-engine";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public CodeUnderstandingEnginePolicy Policy { get; init; } = new();
}

public sealed class CodeUnderstandingEnginePolicy
{
    public string SchemaVersion { get; init; } = "code-understanding-engine.v2";

    public string PolicyId { get; init; } = "code-understanding-engine";

    public int PolicyVersion { get; init; } = 2;

    public string Summary { get; init; } = string.Empty;

    public string StrengthensTruthRoot { get; init; } = ".ai/codegraph/";

    public CodeUnderstandingExtractionBoundary ExtractionBoundary { get; init; } = new();

    public CodeUnderstandingConcernFamily[] ConcernFamilies { get; init; } = [];

    public CodeUnderstandingPrecisionTier[] PrecisionTiers { get; init; } = [];

    public CodeUnderstandingSemanticPathPilot[] SemanticPathPilots { get; init; } = [];

    public RuntimeKernelTruthRootDescriptor[] TruthRoots { get; init; } = [];

    public RuntimeKernelBoundaryRule[] BoundaryRules { get; init; } = [];

    public RuntimeKernelGovernedReadPath[] GovernedReadPaths { get; init; } = [];

    public CodeUnderstandingQualificationLine Qualification { get; init; } = new();

    public string[] Notes { get; init; } = [];
}

public sealed class CodeUnderstandingExtractionBoundary
{
    public string[] DirectAbsorptions { get; init; } = [];

    public string[] TranslatedIntoCarves { get; init; } = [];

    public string[] RejectedAnchors { get; init; } = [];
}

public sealed class CodeUnderstandingConcernFamily
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

public sealed class CodeUnderstandingPrecisionTier
{
    public string TierId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string PrimaryReadPath { get; init; } = string.Empty;

    public string EscalationTrigger { get; init; } = string.Empty;

    public string[] EvidenceRefs { get; init; } = [];

    public string[] NonClaims { get; init; } = [];
}

public sealed class CodeUnderstandingSemanticPathPilot
{
    public string PilotId { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public string CurrentStatus { get; init; } = string.Empty;

    public string[] TargetPrecisionTiers { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string[] ScopeRoots { get; init; } = [];

    public string[] GovernedEntryPoints { get; init; } = [];

    public string[] EvidenceRefs { get; init; } = [];

    public string[] Guardrails { get; init; } = [];

    public string[] NonClaims { get; init; } = [];
}

public sealed class CodeUnderstandingQualificationLine
{
    public string Summary { get; init; } = string.Empty;

    public string[] ProvenLayers { get; init; } = [];

    public string[] FirstResponseRules { get; init; } = [];

    public string[] RemainingGaps { get; init; } = [];

    public string[] SuccessCriteria { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}
