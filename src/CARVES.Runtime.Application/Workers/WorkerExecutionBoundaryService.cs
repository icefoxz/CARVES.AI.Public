using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerExecutionBoundaryService
{
    private readonly string repoRoot;
    private readonly RepoRegistryService repoRegistryService;
    private readonly PlatformGovernanceService governanceService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;
    private readonly RuntimePolicyBundleService? runtimePolicyBundleService;

    public WorkerExecutionBoundaryService(
        string repoRoot,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService,
        WorkerOperationalPolicyService operationalPolicyService,
        RuntimePolicyBundleService? runtimePolicyBundleService = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.repoRegistryService = repoRegistryService;
        this.governanceService = governanceService;
        this.operationalPolicyService = operationalPolicyService;
        this.runtimePolicyBundleService = runtimePolicyBundleService;
    }

    public WorkerExecutionBoundaryService(
        string repoRoot,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService)
        : this(repoRoot, repoRegistryService, governanceService, new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
    {
    }

    public WorkerExecutionProfile ResolveProfile(string backendId)
    {
        return ResolveProfile(backendId, task: null, repoId: null);
    }

    public WorkerExecutionProfile ResolveProfile(string backendId, Carves.Runtime.Domain.Tasks.TaskNode? task, string? repoId)
    {
        var repoPolicy = ResolveRepoPolicy(repoId);
        var workerPolicy = governanceService.ResolveWorkerPolicy(repoPolicy.WorkerPolicyProfile);
        var profiles = LoadProfiles(workerPolicy);
        var desiredProfile = ResolveDesiredProfile(task, repoId);
        if (profiles.Any(profile => string.Equals(profile.ProfileId, desiredProfile.ProfileId, StringComparison.Ordinal)))
        {
            return desiredProfile;
        }

        var preferredProfileId = repoPolicy.ManualApprovalMode ? "sandbox_readonly" : workerPolicy.DefaultProfileId;

        return profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, preferredProfileId, StringComparison.Ordinal))
            ?? profiles.FirstOrDefault(profile => profile.Trusted)
            ?? profiles.FirstOrDefault()
            ?? WorkerExecutionProfile.UntrustedDefault;
    }

    public WorkerExecutionProfile ResolveDesiredProfile(Carves.Runtime.Domain.Tasks.TaskNode? task = null, string? repoId = null)
    {
        var repoPolicy = ResolveRepoPolicy(repoId);
        var workerPolicy = governanceService.ResolveWorkerPolicy(repoPolicy.WorkerPolicyProfile);
        var profiles = LoadProfiles(workerPolicy);
        var unregisteredLocalRepo = IsUnregisteredLocalRepo(repoId);

        var explicitProfileId = task is not null && task.Metadata.TryGetValue("worker_trust_profile", out var taskProfileId)
            ? taskProfileId
            : null;
        var runtimeSelectionPolicy = runtimePolicyBundleService?.LoadWorkerSelectionPolicy();
        var defaultProfileId = !string.IsNullOrWhiteSpace(runtimeSelectionPolicy?.DefaultTrustProfileId)
            ? runtimeSelectionPolicy.DefaultTrustProfileId
            : !string.IsNullOrWhiteSpace(repoPolicy.PreferredTrustProfileId)
                ? repoPolicy.PreferredTrustProfileId
                : workerPolicy.DefaultProfileId;
        var preferredProfileId = repoPolicy.ManualApprovalMode
            ? "sandbox_readonly"
            : !string.IsNullOrWhiteSpace(explicitProfileId)
                ? explicitProfileId
                : unregisteredLocalRepo
                    ? workerPolicy.DefaultProfileId
                    : !string.IsNullOrWhiteSpace(runtimeSelectionPolicy?.DefaultTrustProfileId)
                        ? runtimeSelectionPolicy.DefaultTrustProfileId
                        : operationalPolicyService.ResolvePreferredTrustProfileId(repoId, defaultProfileId);

        return profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, preferredProfileId, StringComparison.Ordinal))
            ?? profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, workerPolicy.DefaultProfileId, StringComparison.Ordinal))
            ?? profiles.FirstOrDefault()
            ?? WorkerExecutionProfile.UntrustedDefault;
    }

    public IReadOnlyList<WorkerExecutionProfile> ListProfiles(string? repoId = null)
    {
        var repoPolicy = ResolveRepoPolicy(repoId);
        var workerPolicy = governanceService.ResolveWorkerPolicy(repoPolicy.WorkerPolicyProfile);
        return LoadProfiles(workerPolicy);
    }

    public WorkerExecutionBoundaryDecision Evaluate(WorkerExecutionRequest request)
    {
        var repoDescriptor = repoRegistryService.List()
            .FirstOrDefault(item => string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase));
        var repoPolicy = ResolveRepoPolicy();
        if (repoPolicy.ManualApprovalMode && request.Profile.Trusted)
        {
            RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileDenied, "Repo policy requires manual approval mode, so trusted worker execution is denied.");
            return new WorkerExecutionBoundaryDecision(false, request.Profile, "Repo policy requires manual approval mode, so trusted worker execution is denied.");
        }

        if (request.Profile.Trusted && request.Profile.ApprovalMode != WorkerApprovalMode.Never)
        {
            RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileDenied, "Trusted worker execution requires approval mode 'never'.");
            return new WorkerExecutionBoundaryDecision(false, request.Profile, "Trusted worker execution requires approval mode 'never'.");
        }

        if (!request.Profile.AllowedRepoScopes.Any(scope => scope == "*" || string.Equals(scope, repoPolicy.ProfileId, StringComparison.Ordinal) || string.Equals(scope, request.RepoRoot, StringComparison.OrdinalIgnoreCase)))
        {
            RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileDenied, "Worker execution profile does not allow this repo scope.");
            return new WorkerExecutionBoundaryDecision(false, request.Profile, "Worker execution profile does not allow this repo scope.");
        }

        foreach (var command in request.ValidationCommands)
        {
            var flattened = string.Join(' ', command);
            if (request.Profile.DeniedCommandPrefixes.Any(prefix => flattened.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileDenied, $"Validation command '{flattened}' is denied by worker execution policy.");
                return new WorkerExecutionBoundaryDecision(false, request.Profile, $"Validation command '{flattened}' is denied by worker execution policy.");
            }

            if (request.Profile.AllowedCommandPrefixes.Count > 0
                && !request.Profile.AllowedCommandPrefixes.Any(prefix => flattened.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileDenied, $"Validation command '{flattened}' is outside the worker execution allowlist.");
                return new WorkerExecutionBoundaryDecision(false, request.Profile, $"Validation command '{flattened}' is outside the worker execution allowlist.");
            }
        }

        if (request.Profile.Trusted)
        {
            RecordEvent(repoDescriptor, GovernanceEventType.WorkerTrustedProfileActivated, $"Trusted worker profile '{request.Profile.ProfileId}' enabled for backend '{request.BackendHint}'.");
        }

        return new WorkerExecutionBoundaryDecision(true, request.Profile, $"Worker execution allowed through profile '{request.Profile.ProfileId}'.");
    }

    private RepoPolicy ResolveRepoPolicy(string? repoId = null)
    {
        var descriptor = !string.IsNullOrWhiteSpace(repoId)
            ? repoRegistryService.List().FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            : repoRegistryService.List().FirstOrDefault(item => string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase));
        return descriptor is null
            ? governanceService.ResolveRepoPolicy("balanced")
            : governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
    }

    private bool IsUnregisteredLocalRepo(string? repoId)
    {
        if (!string.IsNullOrWhiteSpace(repoId))
        {
            return false;
        }

        return !repoRegistryService.List().Any(item =>
            string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<WorkerExecutionProfile> DefaultProfiles()
    {
        return
        [
            new WorkerExecutionProfile
            {
                ProfileId = "sandbox_readonly",
                DisplayName = "Sandbox Readonly",
                Description = "Readonly worker profile for inspect, summarize, and low-risk planning execution.",
                Trusted = false,
                SandboxMode = WorkerSandboxMode.ReadOnly,
                ApprovalMode = WorkerApprovalMode.OnRequest,
                NetworkAccessEnabled = false,
                WorkspaceBoundary = "workspace",
                FilesystemScope = "readonly",
                EscalationDefault = "review",
                AllowedPermissionCategories = ["filesystem_read"],
                AllowedRepoScopes = ["*", "balanced"],
                AllowedCommandPrefixes = ["dotnet", "git", "node", "npm", "pwsh", "powershell", "cmd", "python"],
                DeniedCommandPrefixes = ["git reset", "git clean", "Remove-Item", "rm -rf", "del /f", "format "],
            },
            new WorkerExecutionProfile
            {
                ProfileId = "workspace_safe_write",
                DisplayName = "Workspace Safe Write",
                Description = "Governed write profile for scoped edits inside the task workspace.",
                Trusted = false,
                SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                ApprovalMode = WorkerApprovalMode.OnRequest,
                NetworkAccessEnabled = false,
                WorkspaceBoundary = "workspace",
                FilesystemScope = "workspace_safe_write",
                EscalationDefault = "review",
                AllowedPermissionCategories = ["filesystem_read", "filesystem_write"],
                AllowedRepoScopes = ["*", "balanced"],
                AllowedCommandPrefixes = ["dotnet", "git", "node", "npm", "pwsh", "powershell", "cmd", "python"],
                DeniedCommandPrefixes = ["git reset", "git clean", "Remove-Item", "rm -rf", "del /f", "format "],
            },
            new WorkerExecutionProfile
            {
                ProfileId = "workspace_build_test",
                DisplayName = "Workspace Build Test",
                Description = "Governed workspace write profile with build/test validation allowed.",
                Trusted = false,
                SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                ApprovalMode = WorkerApprovalMode.OnRequest,
                NetworkAccessEnabled = false,
                WorkspaceBoundary = "workspace",
                FilesystemScope = "workspace_build_test",
                EscalationDefault = "review",
                AllowedPermissionCategories = ["filesystem_read", "filesystem_write", "process_control"],
                AllowedRepoScopes = ["*", "balanced"],
                AllowedCommandPrefixes = ["dotnet", "git", "node", "npm", "pwsh", "powershell", "cmd", "python"],
                DeniedCommandPrefixes = ["git reset", "git clean", "Remove-Item", "rm -rf", "del /f", "format "],
            },
            new WorkerExecutionProfile
            {
                ProfileId = "extended_dev_ops",
                DisplayName = "Extended Dev Ops",
                Description = "Trusted profile for repo-scoped networked build/test and automation workflows.",
                Trusted = true,
                SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                ApprovalMode = WorkerApprovalMode.Never,
                NetworkAccessEnabled = true,
                WorkspaceBoundary = "workspace",
                FilesystemScope = "workspace_build_test",
                EscalationDefault = "allow_by_policy",
                AllowedPermissionCategories = ["filesystem_read", "filesystem_write", "network_access", "process_control"],
                AllowedRepoScopes = ["*", "balanced"],
                AllowedCommandPrefixes = ["dotnet", "git", "node", "npm", "pwsh", "powershell", "cmd", "python"],
                DeniedCommandPrefixes = ["git reset", "git clean", "Remove-Item", "rm -rf", "del /f", "format "],
            },
            new WorkerExecutionProfile
            {
                ProfileId = "operator_approved_elevated",
                DisplayName = "Operator Approved Elevated",
                Description = "Explicit elevated profile reserved for operator-approved recovery or maintenance flows.",
                Trusted = true,
                SandboxMode = WorkerSandboxMode.DangerFullAccess,
                ApprovalMode = WorkerApprovalMode.OnRequest,
                NetworkAccessEnabled = true,
                WorkspaceBoundary = "operator_override",
                FilesystemScope = "elevated",
                EscalationDefault = "operator_required",
                AllowedPermissionCategories = ["filesystem_read", "filesystem_write", "filesystem_delete", "network_access", "process_control", "secret_access", "elevated_privilege"],
                AllowedRepoScopes = ["*"],
                AllowedCommandPrefixes = ["dotnet", "git", "node", "npm", "pwsh", "powershell", "cmd", "python"],
                DeniedCommandPrefixes = ["format "],
            },
        ];
    }

    private IReadOnlyList<WorkerExecutionProfile> LoadProfiles(WorkerPolicy workerPolicy)
    {
        var externalized = runtimePolicyBundleService?.LoadTrustProfilesPolicy().Profiles;
        var profiles = externalized is { Count: > 0 }
            ? externalized
            : workerPolicy.Profiles.Count == 0
                ? DefaultProfiles()
                : workerPolicy.Profiles;
        return profiles.OrderBy(profile => profile.ProfileId, StringComparer.Ordinal).ToArray();
    }

    private void RecordEvent(RepoDescriptor? descriptor, GovernanceEventType eventType, string message)
    {
        if (descriptor is null)
        {
            return;
        }

        governanceService.RecordEvent(eventType, descriptor.RepoId, message);
    }
}
