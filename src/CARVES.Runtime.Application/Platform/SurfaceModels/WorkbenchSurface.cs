using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class WorkbenchOverviewReadModel
{
    public string SurfaceId { get; init; } = "workbench-overview";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string RepoRoot { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string SessionStatus { get; init; } = "none";

    public string HostControlState { get; init; } = "running";

    public string Actionability { get; init; } = "none";

    public string? WaitingReason { get; init; }

    public string? CurrentCardId { get; init; }

    public string? CurrentTaskId { get; init; }

    public string? NextTaskId { get; init; }

    public string CurrentMode { get; init; } = "mode_a_open_repo_advisory";

    public string ExternalAgentRecommendedMode { get; init; } = "mode_a_open_repo_advisory";

    public string ExternalAgentRecommendationPosture { get; init; } = "advisory_until_formal_planning";

    public string ExternalAgentRecommendationSummary { get; init; } = string.Empty;

    public string ExternalAgentRecommendedAction { get; init; } = string.Empty;

    public string ExternalAgentConstraintTier { get; init; } = "soft_advisory";

    public string ExternalAgentConstraintSummary { get; init; } = string.Empty;

    public int ExternalAgentStrongerModeBlockerCount { get; init; }

    public string? ExternalAgentFirstStrongerModeBlockerId { get; init; }

    public string? ExternalAgentFirstStrongerModeBlockerTargetMode { get; init; }

    public string? ExternalAgentFirstStrongerModeBlockerRequiredAction { get; init; }

    public string? ExternalAgentFirstStrongerModeBlockerConstraintClass { get; init; }

    public string? ExternalAgentFirstStrongerModeBlockerEnforcementLevel { get; init; }

    public string ModeEOperationalActivationState { get; init; } = "plan_init_required_before_mode_e_activation";

    public string ModeEOperationalActivationSummary { get; init; } = string.Empty;

    public string? ModeEActivationTaskId { get; init; }

    public string? ModeEActivationResultReturnChannel { get; init; }

    public IReadOnlyList<string> ModeEActivationCommands { get; init; } = Array.Empty<string>();

    public string ModeEActivationRecommendedNextAction { get; init; } = string.Empty;

    public int ModeEActivationBlockingCheckCount { get; init; }

    public string? ModeEActivationFirstBlockingCheckId { get; init; }

    public string? ModeEActivationFirstBlockingCheckSummary { get; init; }

    public string? ModeEActivationFirstBlockingCheckRequiredAction { get; init; }

    public string ModeEActivationPlaybookSummary { get; init; } = string.Empty;

    public int ModeEActivationPlaybookStepCount { get; init; }

    public string? ModeEActivationFirstPlaybookStepCommand { get; init; }

    public string? ModeEActivationFirstPlaybookStepSummary { get; init; }

    public string PlanningCouplingPosture { get; init; } = "p0_passive_guidance";

    public string FormalPlanningPosture { get; init; } = "discussion_only";

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

    public IReadOnlyList<string> ActivePlanningCardFillMissingFieldPaths { get; init; } = Array.Empty<string>();

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public string ManagedWorkspacePosture { get; init; } = "plan_init_required_before_managed_workspace_issuance";

    public string VendorNativeAccelerationPosture { get; init; } = "blocked_by_vendor_native_acceleration_gaps";

    public string CodexReinforcementState { get; init; } = "repo_guard_assets_incomplete";

    public string ClaudeReinforcementState { get; init; } = "bounded_runtime_qualification_incomplete";

    public int ReadyTaskCount { get; init; }

    public int RunningTaskCount { get; init; }

    public int ReviewTaskCount { get; init; }

    public int BlockedTaskCount { get; init; }

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

    public int PendingApprovalCount { get; init; }

    public IReadOnlyList<WorkbenchTaskListItem> FocusTasks { get; init; } = Array.Empty<WorkbenchTaskListItem>();

    public IReadOnlyList<WorkbenchActionDescriptor> AvailableActions { get; init; } = Array.Empty<WorkbenchActionDescriptor>();
}

public sealed class CardWorkbenchReadModel
{
    public string SurfaceId { get; init; } = "workbench-card";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string LifecycleState { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public WorkbenchRealityReadModel Reality { get; init; } = new();

    public string BlockedReason { get; init; } = string.Empty;

    public string NextAction { get; init; } = string.Empty;

    public IReadOnlyList<WorkbenchTaskListItem> Tasks { get; init; } = Array.Empty<WorkbenchTaskListItem>();

    public IReadOnlyList<WorkbenchActionDescriptor> AvailableActions { get; init; } = Array.Empty<WorkbenchActionDescriptor>();
}

public sealed class TaskWorkbenchReadModel
{
    public string SurfaceId { get; init; } = "workbench-task";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public WorkbenchRealityReadModel Reality { get; init; } = new();

    public string BlockedReason { get; init; } = string.Empty;

    public string NextAction { get; init; } = string.Empty;

    public WorkbenchReviewEvidenceReadModel ReviewEvidence { get; init; } = new();

    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> UnresolvedDependencies { get; init; } = Array.Empty<string>();

    public WorkbenchRunSummary? ExecutionRun { get; init; }

    public IReadOnlyList<WorkbenchArtifactReference> Artifacts { get; init; } = Array.Empty<WorkbenchArtifactReference>();

    public IReadOnlyList<WorkbenchTaskListItem> RelatedTasks { get; init; } = Array.Empty<WorkbenchTaskListItem>();

    public IReadOnlyList<WorkbenchActionDescriptor> AvailableActions { get; init; } = Array.Empty<WorkbenchActionDescriptor>();
}

public sealed class ReviewWorkbenchReadModel
{
    public string SurfaceId { get; init; } = "workbench-review";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<WorkbenchReviewQueueItem> ReviewQueue { get; init; } = Array.Empty<WorkbenchReviewQueueItem>();

    public IReadOnlyList<WorkbenchReviewQueueItem> TaskActionQueue { get; init; } = Array.Empty<WorkbenchReviewQueueItem>();

    public IReadOnlyList<WorkbenchActionDescriptor> GlobalActions { get; init; } = Array.Empty<WorkbenchActionDescriptor>();
}

public sealed class WorkbenchTaskListItem
{
    public string TaskId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public WorkbenchRealityReadModel Reality { get; init; } = new();

    public string NextAction { get; init; } = string.Empty;

    public string BlockedReason { get; init; } = string.Empty;
}

public sealed class WorkbenchReviewQueueItem
{
    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public WorkbenchRealityReadModel Reality { get; init; } = new();

    public WorkbenchReviewEvidenceReadModel ReviewEvidence { get; init; } = new();

    public IReadOnlyList<WorkbenchActionDescriptor> AvailableActions { get; init; } = Array.Empty<WorkbenchActionDescriptor>();
}

public sealed class WorkbenchReviewEvidenceReadModel
{
    public string Status { get; init; } = "unavailable";

    public bool CanFinalApprove { get; init; }

    public string ClosureStatus { get; init; } = "not_evaluated";

    public bool ClosureWritebackAllowed { get; init; }

    public string Summary { get; init; } = "(none)";

    public IReadOnlyList<string> MissingEvidence { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ClosureBlockers { get; init; } = Array.Empty<string>();

    public string CompletionClaimStatus { get; init; } = "not_recorded";

    public bool CompletionClaimRequired { get; init; }

    public string CompletionClaimSummary { get; init; } = "(none)";

    public IReadOnlyList<string> CompletionClaimMissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CompletionClaimEvidencePaths { get; init; } = Array.Empty<string>();

    public string HostValidationStatus { get; init; } = "not_evaluated";

    public bool HostValidationRequired { get; init; }

    public string HostValidationSummary { get; init; } = "(none)";

    public IReadOnlyList<string> HostValidationBlockers { get; init; } = Array.Empty<string>();
}

public sealed class WorkbenchRealityReadModel
{
    public string Status { get; init; } = "ghost";

    public string Summary { get; init; } = "(none)";
}

public sealed class WorkbenchActionDescriptor
{
    public string ActionId { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public bool RequiresReason { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class WorkbenchRunSummary
{
    public string RunId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int RunCount { get; init; }

    public int CurrentStepIndex { get; init; }

    public string CurrentStepTitle { get; init; } = string.Empty;
}

public sealed class WorkbenchArtifactReference
{
    public string Label { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}
