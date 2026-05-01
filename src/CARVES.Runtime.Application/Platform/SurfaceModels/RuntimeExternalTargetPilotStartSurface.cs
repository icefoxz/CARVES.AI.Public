namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeExternalTargetPilotStartSurface
{
    public string SchemaVersion { get; init; } = "runtime-external-target-pilot-start.v1";

    public string SurfaceId { get; init; } = "runtime-external-target-pilot-start";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string QuickstartGuideDocumentPath { get; init; } = "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md";

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot start";

    public string JsonCommandEntry { get; init; } = "carves pilot start --json";

    public string NextCommandEntry { get; init; } = "carves pilot next";

    public string NextJsonCommandEntry { get; init; } = "carves pilot next --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-external-target-pilot-start";

    public string ApiCommandEntry { get; init; } = "carves api runtime-external-target-pilot-start";

    public bool PilotStartBundleReady { get; init; }

    public bool AlphaExternalUseReady { get; init; }

    public bool InvocationContractComplete { get; init; }

    public bool ExternalConsumerResourcePackComplete { get; init; }

    public bool GovernedAgentHandoffReady { get; init; }

    public bool ProductPilotStatusValid { get; init; }

    public bool RuntimeInitialized { get; init; }

    public bool TargetAgentBootstrapReady { get; init; }

    public bool TargetReadyForFormalPlanning { get; init; }

    public string RecommendedInvocationMode { get; init; } = string.Empty;

    public string RuntimeRootKind { get; init; } = string.Empty;

    public string CandidateDistRoot { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public int CurrentStageOrder { get; init; }

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public bool LegacyNextCommandProjectionOnly { get; init; } = true;

    public bool LegacyNextCommandDoNotAutoRun { get; init; } = true;

    public string PreferredActionSource { get; init; } = "available_actions";

    public bool DiscussionFirstSurface { get; init; }

    public bool AutoRunAllowed { get; init; }

    public string? RecommendedActionId { get; init; }

    public IReadOnlyList<RuntimeInteractionActionSurface> AvailableActions { get; init; } = [];

    public IReadOnlyList<string> ForbiddenAutoActions { get; init; } = [];

    public string PilotStatusPosture { get; init; } = string.Empty;

    public IReadOnlyList<string> StartReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> AgentOperatingRules { get; init; } = [];

    public IReadOnlyList<string> StopAndReportTriggers { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeExternalTargetPilotNextSurface
{
    public string SchemaVersion { get; init; } = "runtime-external-target-pilot-next.v1";

    public string SurfaceId { get; init; } = "runtime-external-target-pilot-next";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string QuickstartGuideDocumentPath { get; init; } = "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md";

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot next";

    public string JsonCommandEntry { get; init; } = "carves pilot next --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-external-target-pilot-next";

    public string ApiCommandEntry { get; init; } = "carves api runtime-external-target-pilot-next";

    public bool ReadyToRunNextCommand { get; init; }

    public bool AlphaExternalUseReady { get; init; }

    public bool RuntimeInitialized { get; init; }

    public string CurrentStageId { get; init; } = string.Empty;

    public int CurrentStageOrder { get; init; }

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public bool LegacyNextCommandProjectionOnly { get; init; } = true;

    public bool LegacyNextCommandDoNotAutoRun { get; init; } = true;

    public string PreferredActionSource { get; init; } = "available_actions";

    public bool DiscussionFirstSurface { get; init; }

    public bool AutoRunAllowed { get; init; }

    public string? RecommendedActionId { get; init; }

    public IReadOnlyList<RuntimeInteractionActionSurface> AvailableActions { get; init; } = [];

    public IReadOnlyList<string> ForbiddenAutoActions { get; init; } = [];

    public string PilotStatusPosture { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> StopAndReportTriggers { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
