using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerExecutionProfile
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string ProfileId { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool Trusted { get; init; }

    public WorkerSandboxMode SandboxMode { get; init; } = WorkerSandboxMode.WorkspaceWrite;

    public WorkerApprovalMode ApprovalMode { get; init; } = WorkerApprovalMode.Untrusted;

    public bool NetworkAccessEnabled { get; init; }

    public string WorkspaceBoundary { get; init; } = "workspace";

    public string FilesystemScope { get; init; } = "readonly";

    public string EscalationDefault { get; init; } = "review";

    public IReadOnlyList<string> AllowedPermissionCategories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedRepoScopes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedCommandPrefixes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DeniedCommandPrefixes { get; init; } = Array.Empty<string>();

    public static WorkerExecutionProfile UntrustedDefault { get; } = new()
    {
        ProfileId = "untrusted-default",
        DisplayName = "Untrusted Default",
        Description = "Default untrusted worker execution profile.",
        Trusted = false,
        SandboxMode = WorkerSandboxMode.ReadOnly,
        ApprovalMode = WorkerApprovalMode.Untrusted,
        NetworkAccessEnabled = false,
        WorkspaceBoundary = "workspace",
        FilesystemScope = "readonly",
        EscalationDefault = "review",
        AllowedPermissionCategories = ["filesystem_read"],
        AllowedRepoScopes = ["*"],
        AllowedCommandPrefixes = [],
        DeniedCommandPrefixes = ["git reset", "git clean", "git checkout", "Remove-Item", "rm", "del"],
    };
}
