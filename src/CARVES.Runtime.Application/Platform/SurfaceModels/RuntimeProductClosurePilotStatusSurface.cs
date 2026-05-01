namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeProductClosurePilotStatusSurface
{
    public string SchemaVersion { get; init; } = "runtime-product-closure-pilot-status.v1";

    public string SurfaceId { get; init; } = "runtime-product-closure-pilot-status";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string OperationalState { get; init; } = string.Empty;

    public bool SafeToStartNewExecution { get; init; } = true;

    public bool SafeToDiscuss { get; init; } = true;

    public bool SafeToCleanup { get; init; } = true;

    public string CurrentStageId { get; init; } = string.Empty;

    public int CurrentStageOrder { get; init; }

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string NextCommand { get; init; } = string.Empty;

    public bool LegacyNextCommandProjectionOnly { get; init; } = true;

    public bool LegacyNextCommandDoNotAutoRun { get; init; } = true;

    public string PreferredActionSource { get; init; } = "available_actions";

    public bool DiscussionFirstSurface { get; init; }

    public bool AutoRunAllowed { get; init; }

    public string? RecommendedActionId { get; init; }

    public IReadOnlyList<RuntimeInteractionActionSurface> AvailableActions { get; init; } = [];

    public IReadOnlyList<string> ForbiddenAutoActions { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public bool RuntimeInitialized { get; init; }

    public string TargetAgentBootstrapPosture { get; init; } = string.Empty;

    public string TargetAgentBootstrapRecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> TargetAgentBootstrapMissingFiles { get; init; } = [];

    public string FormalPlanningState { get; init; } = string.Empty;

    public string FormalPlanningPosture { get; init; } = string.Empty;

    public string ManagedWorkspacePosture { get; init; } = string.Empty;

    public int ActiveLeaseCount { get; init; }

    public bool RecoverableCleanupRequired { get; init; }

    public int RecoverableResidueCount { get; init; }

    public string HighestRecoverableResidueSeverity { get; init; } = "none";

    public bool RecoverableResidueBlocksAutoRun { get; init; }

    public string RecoverableCleanupActionId { get; init; } = string.Empty;

    public string RecoverableCleanupActionMode { get; init; } = "none";

    public string RecoverableCleanupSummary { get; init; } = string.Empty;

    public string RecoverableCleanupRecommendedNextAction { get; init; } = string.Empty;

    public string TargetCommitClosurePosture { get; init; } = string.Empty;

    public string TargetCommitClosureRecommendedNextAction { get; init; } = string.Empty;

    public bool TargetGitWorktreeClean { get; init; }

    public bool TargetCommitClosureComplete { get; init; }

    public string TargetResiduePolicyPosture { get; init; } = string.Empty;

    public bool TargetResiduePolicyReady { get; init; }

    public string TargetIgnoreDecisionPlanPosture { get; init; } = string.Empty;

    public bool TargetIgnoreDecisionPlanReady { get; init; }

    public bool IgnoreDecisionRequired { get; init; }

    public string TargetIgnoreDecisionRecordPosture { get; init; } = string.Empty;

    public bool TargetIgnoreDecisionRecordReady { get; init; }

    public bool TargetIgnoreDecisionRecordAuditReady { get; init; }

    public bool TargetIgnoreDecisionRecordCommitReady { get; init; }

    public int MissingIgnoreDecisionEntryCount { get; init; }

    public int InvalidIgnoreDecisionRecordCount { get; init; }

    public int MalformedIgnoreDecisionRecordCount { get; init; }

    public int ConflictingIgnoreDecisionEntryCount { get; init; }

    public int UncommittedIgnoreDecisionRecordCount { get; init; }

    public string LocalDistFreshnessSmokePosture { get; init; } = string.Empty;

    public string LocalDistFreshnessSmokeRecommendedNextAction { get; init; } = string.Empty;

    public bool LocalDistFreshnessSmokeReady { get; init; }

    public string TargetDistBindingPlanPosture { get; init; } = string.Empty;

    public string TargetDistBindingPlanRecommendedNextAction { get; init; } = string.Empty;

    public string LocalDistHandoffPosture { get; init; } = string.Empty;

    public string LocalDistHandoffRecommendedNextAction { get; init; } = string.Empty;

    public bool StableExternalConsumptionReady { get; init; }

    public string RuntimeRootKind { get; init; } = string.Empty;

    public string FrozenDistTargetReadbackProofPosture { get; init; } = string.Empty;

    public string FrozenDistTargetReadbackProofRecommendedNextAction { get; init; } = string.Empty;

    public bool FrozenDistTargetReadbackProofComplete { get; init; }

    public int TaskCount { get; init; }

    public int ReviewTaskCount { get; init; }

    public int CompletedTaskCount { get; init; }

    public IReadOnlyList<RuntimeProductClosurePilotStatusStageSurface> StageStatuses { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeProductClosurePilotStatusStageSurface
{
    public int Order { get; init; }

    public string StageId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}
