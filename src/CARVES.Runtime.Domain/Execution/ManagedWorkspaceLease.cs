using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class ManagedWorkspaceLease
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string LeaseId { get; init; } = $"managed-workspace-lease-{Guid.NewGuid():N}";

    public string WorkspaceId { get; init; } = $"workspace-{Guid.NewGuid():N}";

    public string PlanHandle { get; init; } = string.Empty;

    public string PlanningSlotId { get; init; } = string.Empty;

    public string PlanningCardId { get; init; } = string.Empty;

    public string SourceIntentDraftId { get; init; } = string.Empty;

    public string? SourceCandidateCardId { get; init; }

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string WorkspacePath { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string BaseCommit { get; init; } = string.Empty;

    public string? WorktreeRuntimeRecordId { get; init; }

    public ManagedWorkspaceLeaseStatus Status { get; set; } = ManagedWorkspaceLeaseStatus.Active;

    public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedOperationClasses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedToolsOrAdapters { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ManagedWorkspacePathPolicyRule> PathPolicies { get; init; } = Array.Empty<ManagedWorkspacePathPolicyRule>();

    public string ApprovalPosture { get; init; } = string.Empty;

    public string CleanupPosture { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddHours(24);

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ManagedWorkspaceLeaseStatus
{
    Active,
    Superseded,
    Released,
    Expired,
}

public sealed class ManagedWorkspacePathPolicyRule
{
    public string PolicyClass { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string EnforcementEffect { get; init; } = string.Empty;

    public IReadOnlyList<string> Examples { get; init; } = Array.Empty<string>();
}

public sealed class ManagedWorkspaceLeaseSnapshot
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public IReadOnlyList<ManagedWorkspaceLease> Leases { get; init; } = Array.Empty<ManagedWorkspaceLease>();
}
