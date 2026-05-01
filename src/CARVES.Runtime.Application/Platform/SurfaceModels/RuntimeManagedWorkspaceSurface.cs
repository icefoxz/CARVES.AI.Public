namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeManagedWorkspaceSurface
{
    public string SchemaVersion { get; init; } = "runtime-managed-workspace.v1";

    public string SurfaceId { get; init; } = "runtime-managed-workspace";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyDocumentPath { get; init; } = string.Empty;

    public string WorkingModesDocumentPath { get; init; } = string.Empty;

    public string ImplementationPlanPath { get; init; } = string.Empty;

    public string ModeDHardeningDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string OperationalState { get; init; } = string.Empty;

    public bool SafeToStartNewExecution { get; init; } = true;

    public bool SafeToDiscuss { get; init; } = true;

    public bool SafeToCleanup { get; init; } = true;

    public string HardModeBaseline { get; init; } = "task_bound_workspace";

    public string ModeDProfileId { get; init; } = "mode_d_scoped_task_workspace_hardening";

    public string ModeDHardeningState { get; init; } = "not_evaluated";

    public string ScopedWorkspaceBoundary { get; init; } = "agent_writes_leased_workspace_official_truth_host_routed";

    public string OfficialTruthIngressPolicy { get; init; } = "host_routed_review_and_writeback_required";

    public string ReplanTriggerPolicy { get; init; } = "scope_escape_requires_replan_before_writeback";

    public string IdeAgentPosture { get; init; } = "portable_cli_first_ide_consumable";

    public string PathPolicyEnforcementState { get; init; } = "projected_only";

    public string PathPolicyEnforcementSummary { get; init; } = string.Empty;

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public string? FormalPlanningState { get; init; }

    public IReadOnlyList<string> BoundTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeManagedWorkspaceLeaseSurface> ActiveLeases { get; init; } = Array.Empty<RuntimeManagedWorkspaceLeaseSurface>();

    public string RecoverableResiduePosture { get; init; } = "no_recoverable_runtime_residue";

    public int RecoverableResidueCount { get; init; }

    public string HighestRecoverableResidueSeverity { get; init; } = "none";

    public bool RecoverableResidueBlocksAutoRun { get; init; }

    public string RecoverableResidueSummary { get; init; } = string.Empty;

    public string RecoverableResidueRecommendedNextAction { get; init; } = string.Empty;

    public string RecoverableCleanupActionId { get; init; } = string.Empty;

    public string RecoverableCleanupActionMode { get; init; } = "none";

    public IReadOnlyList<RuntimeInteractionActionSurface> AvailableActions { get; init; } = Array.Empty<RuntimeInteractionActionSurface>();

    public IReadOnlyList<RuntimeManagedWorkspaceResidueSurface> RecoverableResidues { get; init; } = Array.Empty<RuntimeManagedWorkspaceResidueSurface>();

    public IReadOnlyList<RuntimeManagedWorkspacePathPolicySurface> PathPolicies { get; init; } = Array.Empty<RuntimeManagedWorkspacePathPolicySurface>();

    public IReadOnlyList<RuntimeManagedWorkspaceHardeningCheckSurface> ModeDHardeningChecks { get; init; } = Array.Empty<RuntimeManagedWorkspaceHardeningCheckSurface>();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NonClaims { get; init; } = Array.Empty<string>();
}

public sealed class RuntimeManagedWorkspaceLeaseSurface
{
    public string LeaseId { get; init; } = string.Empty;

    public string WorkspaceId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string WorkspacePath { get; init; } = string.Empty;

    public string BaseCommit { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ApprovalPosture { get; init; } = string.Empty;

    public string CleanupPosture { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedOperationClasses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedToolsOrAdapters { get; init; } = Array.Empty<string>();
}

public sealed class RuntimeManagedWorkspaceResidueSurface
{
    public string ResidueId { get; init; } = string.Empty;

    public string ResidueClass { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Severity { get; init; } = "warning";

    public string? LeaseId { get; init; }

    public string? TaskId { get; init; }

    public string? WorkspacePath { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool Recoverable { get; init; } = true;

    public bool BlocksAutoRun { get; init; }

    public bool BlocksHealthyIdle { get; init; }
}

public sealed class RuntimeManagedWorkspacePathPolicySurface
{
    public string PolicyClass { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string EnforcementEffect { get; init; } = string.Empty;

    public IReadOnlyList<string> Examples { get; init; } = Array.Empty<string>();
}

public sealed class RuntimeManagedWorkspaceHardeningCheckSurface
{
    public string CheckId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;
}
