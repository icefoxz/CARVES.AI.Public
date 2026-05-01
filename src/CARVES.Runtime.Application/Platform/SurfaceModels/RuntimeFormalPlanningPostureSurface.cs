namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeFormalPlanningPostureSurface
{
    public string SchemaVersion { get; init; } = "runtime-formal-planning-posture.v1";

    public string SurfaceId { get; init; } = "runtime-formal-planning-posture";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PlanModeDocumentPath { get; init; } = string.Empty;

    public string PlanningPacketDocumentPath { get; init; } = string.Empty;

    public string PlanningGateDocumentPath { get; init; } = string.Empty;

    public string ManagedWorkspaceDocumentPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string IntentState { get; init; } = string.Empty;

    public string? GuidedPlanningPosture { get; init; }

    public string FormalPlanningState { get; init; } = string.Empty;

    public string FormalPlanningEntryTriggerState { get; init; } = "discussion_only";

    public string FormalPlanningEntryCommand { get; init; } = "plan init [candidate-card-id]";

    public string FormalPlanningEntryRecommendedNextAction { get; init; } = string.Empty;

    public string FormalPlanningEntrySummary { get; init; } = string.Empty;

    public string ActivePlanningSlotState { get; init; } = "no_intent_draft";

    public bool ActivePlanningSlotCanInitialize { get; init; }

    public string ActivePlanningSlotConflictReason { get; init; } = string.Empty;

    public string ActivePlanningSlotRemediationAction { get; init; } = string.Empty;

    public string PlanningCardInvariantState { get; init; } = "no_active_planning_card";

    public bool PlanningCardInvariantCanExportGovernedTruth { get; init; }

    public string PlanningCardInvariantSummary { get; init; } = string.Empty;

    public string PlanningCardInvariantRemediationAction { get; init; } = string.Empty;

    public int PlanningCardInvariantBlockCount { get; init; }

    public int PlanningCardInvariantViolationCount { get; init; }

    public string ActivePlanningCardFillState { get; init; } = "no_active_planning_card";

    public string ActivePlanningCardFillCompletionPosture { get; init; } = "plan_init_required";

    public bool ActivePlanningCardFillReadyForRecommendedExport { get; init; }

    public string ActivePlanningCardFillSummary { get; init; } = string.Empty;

    public string ActivePlanningCardFillRecommendedNextAction { get; init; } = string.Empty;

    public string? ActivePlanningCardFillNextMissingFieldPath { get; init; }

    public int ActivePlanningCardFillRequiredFieldCount { get; init; }

    public int ActivePlanningCardFillMissingRequiredFieldCount { get; init; }

    public IReadOnlyList<string> ActivePlanningCardFillMissingFieldPaths { get; init; } = [];

    public string CurrentMode { get; init; } = string.Empty;

    public string PlanningCouplingPosture { get; init; } = string.Empty;

    public string PlanningCouplingSummary { get; init; } = string.Empty;

    public string? PlanningSlotId { get; init; }

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public bool PacketAvailable { get; init; }

    public string? PacketSummary { get; init; }

    public string? RecommendedNextAction { get; init; }

    public string? Rationale { get; init; }

    public string? NextActionPosture { get; init; }

    public bool ReplanRequired { get; init; }

    public string ManagedWorkspacePosture { get; init; } = string.Empty;

    public string PathPolicyEnforcementState { get; init; } = string.Empty;

    public int ActiveLeaseCount { get; init; }

    public IReadOnlyList<string> ActiveLeaseTaskIds { get; init; } = [];

    public string DispatchState { get; init; } = string.Empty;

    public int AcceptanceContractGapCount { get; init; }

    public int PlanRequiredBlockCount { get; init; }

    public int WorkspaceRequiredBlockCount { get; init; }

    public string? ModeExecutionEntryFirstBlockedTaskId { get; init; }

    public string? ModeExecutionEntryFirstBlockingCheckId { get; init; }

    public string? ModeExecutionEntryFirstBlockingCheckSummary { get; init; }

    public string? ModeExecutionEntryFirstBlockingCheckRequiredAction { get; init; }

    public string? ModeExecutionEntryFirstBlockingCheckRequiredCommand { get; init; }

    public string? ModeExecutionEntryRecommendedNextAction { get; init; }

    public string? ModeExecutionEntryRecommendedNextCommand { get; init; }

    public IReadOnlyList<string> MissingPrerequisites { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
