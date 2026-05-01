namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeDefaultWorkflowProofSurface
{
    public string SchemaVersion { get; init; } = "runtime-default-workflow-proof.v1";

    public string SurfaceId { get; init; } = "runtime-default-workflow-proof";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public bool WorkflowProofComplete { get; init; }

    public bool CurrentRuntimeReady { get; init; }

    public string CurrentRuntimePosture { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string NextCommandSource { get; init; } = string.Empty;

    public int DefaultFirstThreadCommandCount { get; init; }

    public int DefaultWarmReorientationCommandCount { get; init; }

    public int OptionalTroubleshootingCommandCount { get; init; }

    public int PostInitializationMarkdownTokens { get; init; }

    public int PostInitializationMarkdownTokenBudget { get; init; }

    public int DeferredGeneratedMarkdownTokens { get; init; }

    public bool ShortContextReady { get; init; }

    public bool MarkdownReadPathWithinBudget { get; init; }

    public bool GovernanceSurfaceCoverageComplete { get; init; }

    public bool ResourcePackCoversDefaultCommands { get; init; }

    public IReadOnlyList<RuntimeDefaultWorkflowProofStep> DefaultPath { get; init; } = [];

    public IReadOnlyList<RuntimeDefaultWorkflowProofStep> WarmPath { get; init; } = [];

    public IReadOnlyList<RuntimeDefaultWorkflowProofStep> OptionalProofAndTroubleshootingPath { get; init; } = [];

    public IReadOnlyList<RuntimeDefaultWorkflowProofCheck> Checks { get; init; } = [];

    public IReadOnlyList<string> StructuralGaps { get; init; } = [];

    public IReadOnlyList<string> CurrentRuntimeBlockers { get; init; } = [];

    public IReadOnlyList<string> EvidenceSourcePaths { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeDefaultWorkflowProofStep
{
    public string StepId { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string SurfaceId { get; init; } = string.Empty;

    public string Lane { get; init; } = string.Empty;

    public bool RequiredInDefaultPath { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Evidence { get; init; } = string.Empty;
}

public sealed class RuntimeDefaultWorkflowProofCheck
{
    public string CheckId { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public bool Blocking { get; init; }

    public string Summary { get; init; } = string.Empty;
}
