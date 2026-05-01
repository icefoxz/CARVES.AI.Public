namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentThreadStartSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-thread-start.v1";

    public string SurfaceId { get; init; } = "runtime-agent-thread-start";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string StartupEntrySource { get; init; } = "runtime_agent_start";

    public string TargetProjectClassification { get; init; } = "not_checked";

    public string TargetClassificationOwner { get; init; } = "carves_agent_start_readback";

    public string TargetClassificationSource { get; init; } = "runtime_document_root_resolution";

    public bool AgentTargetClassificationAllowed { get; init; }

    public string TargetStartupMode { get; init; } = "not_checked";

    public string ExistingProjectHandling { get; init; } = "not_checked";

    public bool StartupBoundaryReady { get; init; } = true;

    public string StartupBoundaryPosture { get; init; } = "startup_boundary_not_checked";

    public IReadOnlyList<string> StartupBoundaryGaps { get; init; } = [];

    public string? TargetBoundRuntimeRoot { get; init; }

    public string TargetRuntimeBindingStatus { get; init; } = "not_checked";

    public string TargetRuntimeBindingSource { get; init; } = "none";

    public bool AgentRuntimeRebindAllowed { get; init; }

    public string RuntimeBindingRule { get; init; } = "Do not edit .ai/runtime.json or .ai/runtime/attach-handshake.json by hand; stop and show CARVES output to the operator if binding is missing or mismatched.";

    public string WorkerExecutionBoundary { get; init; } = "null_worker_current_version_no_api_sdk_worker_execution";

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves agent start";

    public string JsonCommandEntry { get; init; } = "carves agent start --json";

    public string PilotAliasCommandEntry { get; init; } = "carves pilot boot --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-thread-start";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-thread-start";

    public bool ThreadStartReady { get; init; }

    public string OneCommandForNewThread { get; init; } = "carves agent start --json";

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string NextCommandSource { get; init; } = string.Empty;

    public bool LegacyNextCommandProjectionOnly { get; init; } = true;

    public bool LegacyNextCommandDoNotAutoRun { get; init; } = true;

    public string PreferredActionSource { get; init; } = "available_actions";

    public bool DiscussionFirstSurface { get; init; }

    public bool AutoRunAllowed { get; init; }

    public string? RecommendedActionId { get; init; }

    public IReadOnlyList<RuntimeInteractionActionSurface> AvailableActions { get; init; } = [];

    public IReadOnlyList<string> ForbiddenAutoActions { get; init; } = [];

    public string PilotStartPosture { get; init; } = string.Empty;

    public bool PilotStartBundleReady { get; init; }

    public string PilotStatusPosture { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public int CurrentStageOrder { get; init; }

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string PilotStatusNextCommand { get; init; } = string.Empty;

    public string FollowUpGatePosture { get; init; } = string.Empty;

    public bool FollowUpGateReady { get; init; }

    public int AcceptedPlanningItemCount { get; init; }

    public int ReadyForPlanInitCount { get; init; }

    public string FollowUpGateNextCommand { get; init; } = string.Empty;

    public string HandoffPosture { get; init; } = string.Empty;

    public bool GovernedAgentHandoffReady { get; init; }

    public string WorkingModeRecommendationPosture { get; init; } = string.Empty;

    public string ProtectedTruthRootPosture { get; init; } = string.Empty;

    public string AdapterContractPosture { get; init; } = string.Empty;

    public IReadOnlyList<string> MinimalAgentRules { get; init; } = [];

    public IReadOnlyList<string> StopAndReportTriggers { get; init; } = [];

    public IReadOnlyList<string> TroubleshootingReadbacks { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
