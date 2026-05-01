namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentWorkingModesSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-working-modes.v1";

    public string SurfaceId { get; init; } = "runtime-agent-working-modes";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string WorkingModesDocumentPath { get; init; } = string.Empty;

    public string CollaborationPlaneDocumentPath { get; init; } = string.Empty;

    public string CliFirstDocumentPath { get; init; } = string.Empty;

    public string ImplementationPlanPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CompatibilityBaseline { get; init; } = "mode_a_open_repo_advisory";

    public string FirstGeneralHardMode { get; init; } = "mode_c_task_bound_workspace";

    public string PrimaryIdeStrongMode { get; init; } = "mode_d_scoped_task_workspace";

    public string CurrentMode { get; init; } = string.Empty;

    public string CurrentModeSummary { get; init; } = string.Empty;

    public string StrongestRuntimeSupportedMode { get; init; } = string.Empty;

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

    public string PlanningCouplingPosture { get; init; } = string.Empty;

    public string PlanningCouplingSummary { get; init; } = string.Empty;

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public string ManagedWorkspacePosture { get; init; } = string.Empty;

    public string PathPolicyEnforcementState { get; init; } = string.Empty;

    public string CollaborationPlaneSummary { get; init; } = string.Empty;

    public string ModeEOperationalActivationState { get; init; } = string.Empty;

    public string ModeEOperationalActivationSummary { get; init; } = string.Empty;

    public string? ModeEActivationTaskId { get; init; }

    public string? ModeEActivationResultReturnChannel { get; init; }

    public IReadOnlyList<string> ModeEActivationCommands { get; init; } = [];

    public string ModeEActivationRecommendedNextAction { get; init; } = string.Empty;

    public int ModeEActivationBlockingCheckCount { get; init; }

    public string? ModeEActivationFirstBlockingCheckId { get; init; }

    public string? ModeEActivationFirstBlockingCheckSummary { get; init; }

    public string? ModeEActivationFirstBlockingCheckRequiredAction { get; init; }

    public string ModeEActivationPlaybookSummary { get; init; } = string.Empty;

    public int ModeEActivationPlaybookStepCount { get; init; }

    public string? ModeEActivationFirstPlaybookStepCommand { get; init; }

    public string? ModeEActivationFirstPlaybookStepSummary { get; init; }

    public IReadOnlyList<RuntimeAgentWorkingModeActivationPlaybookStepSurface> ModeEActivationPlaybookSteps { get; init; } = [];

    public IReadOnlyList<RuntimeAgentWorkingModeActivationCheckSurface> ModeEActivationChecks { get; init; } = [];

    public IReadOnlyList<RuntimeAgentWorkingModeDescriptorSurface> SupportedModes { get; init; } = [];

    public IReadOnlyList<RuntimeAgentWorkingModeSelectionBlockerSurface> ExternalAgentStrongerModeBlockers { get; init; } = [];

    public IReadOnlyList<RuntimeAgentWorkingModeConstraintClassSurface> ConstraintClasses { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentWorkingModeDescriptorSurface
{
    public string ModeId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ConstraintStrength { get; init; } = string.Empty;

    public string PlanningCoupling { get; init; } = string.Empty;

    public string Portability { get; init; } = string.Empty;

    public string RuntimeStatus { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeAgentWorkingModeActivationCheckSurface
{
    public string CheckId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;
}

public sealed class RuntimeAgentWorkingModeActivationPlaybookStepSurface
{
    public int Order { get; init; }

    public string StepId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string AppliesWhen { get; init; } = string.Empty;
}

public sealed class RuntimeAgentWorkingModeSelectionBlockerSurface
{
    public string BlockerId { get; init; } = string.Empty;

    public string TargetModeId { get; init; } = string.Empty;

    public string State { get; init; } = "blocking";

    public string ConstraintClass { get; init; } = string.Empty;

    public string EnforcementLevel { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;

    public string RequiredCommand { get; init; } = string.Empty;
}

public sealed class RuntimeAgentWorkingModeConstraintClassSurface
{
    public string ClassId { get; init; } = string.Empty;

    public string EnforcementLevel { get; init; } = string.Empty;

    public IReadOnlyList<string> AppliesToModes { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}
