namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentShortContextSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-short-context.v1";

    public string SurfaceId { get; init; } = "runtime-agent-short-context";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public bool ShortContextReady { get; init; }

    public string CommandEntry { get; init; } = "carves agent context";

    public string JsonCommandEntry { get; init; } = "carves agent context --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-short-context";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-short-context";

    public string RequestedTaskId { get; init; } = "N/A";

    public string ResolvedTaskId { get; init; } = "N/A";

    public string TaskResolutionSource { get; init; } = "none";

    public RuntimeAgentShortContextThreadStart ThreadStart { get; init; } = new();

    public RuntimeAgentShortContextBootstrap Bootstrap { get; init; } = new();

    public RuntimeAgentShortContextTask Task { get; init; } = new();

    public RuntimeAgentShortContextPack ContextPack { get; init; } = new();

    public RuntimeAgentShortContextMarkdownBudget MarkdownBudget { get; init; } = new();

    public IReadOnlyList<string> InitializationReadSources { get; init; } = [];

    public IReadOnlyList<string> MinimalAgentRules { get; init; } = [];

    public IReadOnlyList<string> MarkdownEscalationTriggers { get; init; } = [];

    public IReadOnlyList<RuntimeAgentShortContextCommandRef> PrimaryCommands { get; init; } = [];

    public IReadOnlyList<RuntimeAgentShortContextCommandRef> DetailRefs { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentShortContextThreadStart
{
    public string SurfaceId { get; init; } = "runtime-agent-thread-start";

    public bool ThreadStartReady { get; init; }

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string NextCommandSource { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string OneCommandForNewThread { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeAgentShortContextBootstrap
{
    public string SurfaceId { get; init; } = "runtime-agent-bootstrap-packet";

    public string StartupMode { get; init; } = string.Empty;

    public string RecommendedStartupRoute { get; init; } = string.Empty;

    public string CurrentTaskId { get; init; } = "N/A";

    public int ActiveTaskCount { get; init; }

    public string MarkdownPostInitializationMode { get; init; } = string.Empty;

    public string GovernanceBoundary { get; init; } = string.Empty;

    public IReadOnlyList<string> DefaultInspectCommands { get; init; } = [];
}

public sealed class RuntimeAgentShortContextTask
{
    public string State { get; init; } = "not_selected";

    public string SurfaceId { get; init; } = "runtime-agent-task-overlay";

    public string TaskId { get; init; } = "N/A";

    public string CardId { get; init; } = "N/A";

    public string Title { get; init; } = "N/A";

    public string Status { get; init; } = "N/A";

    public string AcceptanceContractStatus { get; init; } = "N/A";

    public int ScopeFileCount { get; init; }

    public IReadOnlyList<string> EditableRoots { get; init; } = [];

    public IReadOnlyList<string> ReadOnlyRoots { get; init; } = [];

    public IReadOnlyList<string> TruthRoots { get; init; } = [];

    public string SafetyLayerSummary { get; init; } = "N/A";

    public IReadOnlyList<string> SafetyNonClaims { get; init; } = [];

    public IReadOnlyList<string> RequiredVerification { get; init; } = [];

    public IReadOnlyList<string> ValidationCommands { get; init; } = [];

    public IReadOnlyList<string> StopConditions { get; init; } = [];

    public string MarkdownReadMode { get; init; } = "N/A";

    public IReadOnlyList<string> TaskScopedMarkdownRefs { get; init; } = [];
}

public sealed class RuntimeAgentShortContextPack
{
    public string State { get; init; } = "not_applicable";

    public string Command { get; init; } = "N/A";

    public string PackId { get; init; } = "N/A";

    public string ArtifactPath { get; init; } = "N/A";

    public string BudgetPosture { get; init; } = "N/A";

    public int UsedTokens { get; init; }

    public int MaxContextTokens { get; init; }

    public IReadOnlyList<string> BudgetReasonCodes { get; init; } = [];

    public IReadOnlyList<string> TopSources { get; init; } = [];
}

public sealed class RuntimeAgentShortContextMarkdownBudget
{
    public string SurfaceId { get; init; } = "runtime-markdown-read-path-budget";

    public string OverallPosture { get; init; } = "N/A";

    public bool WithinBudget { get; init; }

    public int PostInitializationDefaultTokens { get; init; }

    public int PostInitializationMaxTokens { get; init; }

    public int DeferredMarkdownTokens { get; init; }

    public int TaskScopedMarkdownTokens { get; init; }

    public int TaskScopedMaxTokens { get; init; }

    public string Command { get; init; } = "carves api runtime-markdown-read-path-budget";

    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
}

public sealed class RuntimeAgentShortContextCommandRef
{
    public string Command { get; init; } = string.Empty;

    public string SurfaceId { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public string When { get; init; } = string.Empty;
}
