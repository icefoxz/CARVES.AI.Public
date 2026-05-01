using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

public sealed class ApprovalPolicyEngine
{
    private readonly string repoRoot;
    private readonly RepoRegistryService repoRegistryService;
    private readonly PlatformGovernanceService governanceService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;
    private readonly RuntimePolicyBundleService? runtimePolicyBundleService;

    public ApprovalPolicyEngine(
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

    public ApprovalPolicyEngine(
        string repoRoot,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService)
        : this(repoRoot, repoRegistryService, governanceService, new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
    {
    }

    public WorkerPermissionPolicyDecision Evaluate(WorkerRequest request, WorkerPermissionRequest permissionRequest)
    {
        var profile = request.ExecutionRequest?.Profile ?? WorkerExecutionProfile.UntrustedDefault;
        var repoPolicy = ResolveRepoPolicy(request.Selection?.RepoId);
        var policy = runtimePolicyBundleService?.LoadApprovalPolicy() ?? ToRuntimeApprovalPolicy(operationalPolicyService.GetPolicy().Approval);
        var category = MapCategory(permissionRequest.Kind);
        var insideWorkspace = !string.Equals(permissionRequest.ScopeSummary, "outside_workspace", StringComparison.OrdinalIgnoreCase);

        if (permissionRequest.Kind == WorkerPermissionKind.UnknownPermissionRequest)
        {
            return Review("unknown_permission", "Unknown permission requests require operator review.", "Worker execution is paused for an operator decision.");
        }

        if (policy.AutoDenyCategories.Contains(category, StringComparer.Ordinal))
        {
            return Deny("policy_auto_deny", $"Operational policy denies permission category '{category}'.", "Worker execution is denied by the operational approval policy.");
        }

        if (permissionRequest.Kind == WorkerPermissionKind.NetworkAccess && !profile.NetworkAccessEnabled)
        {
            return Deny("network_disabled", $"Profile '{profile.ProfileId}' does not allow network access.", "Worker execution is denied because network access is outside the trust profile.");
        }

        if (permissionRequest.Kind == WorkerPermissionKind.OutsideWorkspaceAccess && !string.Equals(profile.WorkspaceBoundary, "operator_override", StringComparison.Ordinal))
        {
            return Deny("outside_workspace_denied", $"Profile '{profile.ProfileId}' does not allow access outside the workspace boundary.", "Worker execution is denied because the request exceeds the workspace boundary.");
        }

        if (!profile.AllowedPermissionCategories.Contains(category, StringComparer.Ordinal))
        {
            return Deny("permission_not_allowed", $"Profile '{profile.ProfileId}' does not allow permission category '{category}'.", "Worker execution is denied because the permission is outside the allowed profile categories.");
        }

        if (repoPolicy.ManualApprovalMode && policy.ManualApprovalModeRequiresReview)
        {
            return Review("manual_approval_mode", $"Repo policy '{repoPolicy.ProfileId}' requires manual approval mode.", "Worker execution is paused because the repo policy requires operator approval.");
        }

        if (policy.HighRiskRequiresReview && permissionRequest.RiskLevel >= WorkerPermissionRiskLevel.High)
        {
            return Review("high_risk_permission", $"Permission '{permissionRequest.Kind}' has risk level '{permissionRequest.RiskLevel}'.", "Worker execution is paused because the request is high risk.");
        }

        if (!insideWorkspace && policy.OutsideWorkspaceRequiresReview)
        {
            return Review("boundary_review", "Permission request crosses the workspace boundary.", "Worker execution is paused for workspace-boundary review.");
        }

        if (policy.ForceReviewCategories.Contains(category, StringComparer.Ordinal))
        {
            return Review("policy_force_review", $"Operational policy requires review for permission category '{category}'.", "Worker execution is paused by the operational approval policy.");
        }

        if (policy.AutoAllowCategories.Contains(category, StringComparer.Ordinal))
        {
            return Allow("policy_auto_allow", $"Operational policy allows permission category '{category}'.", "Worker execution can continue under the operational approval policy.");
        }

        return Allow("allowed_by_policy", $"Permission '{permissionRequest.Kind}' is allowed for profile '{profile.ProfileId}'.", "Worker execution can continue under policy.");
    }

    public IReadOnlyList<string> DescribePolicySummary(string? repoId = null)
    {
        var repoPolicy = ResolveRepoPolicy(repoId);
        var workerPolicy = governanceService.ResolveWorkerPolicy(repoPolicy.WorkerPolicyProfile);
        return
        [
            $"Repo policy profile: {repoPolicy.ProfileId}",
            $"Manual approval mode: {repoPolicy.ManualApprovalMode}",
            $"Worker policy profile: {repoPolicy.WorkerPolicyProfile}",
            $"Preferred trust profile: {repoPolicy.PreferredTrustProfileId}",
            $"Default worker profile: {workerPolicy.DefaultProfileId}",
            .. (runtimePolicyBundleService?.Describe() ?? Array.Empty<string>()),
            .. operationalPolicyService.Describe(repoId),
        ];
    }

    private Carves.Runtime.Domain.Platform.RepoPolicy ResolveRepoPolicy(string? repoId)
    {
        if (!string.IsNullOrWhiteSpace(repoId))
        {
            var descriptor = repoRegistryService.List().FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));
            if (descriptor is not null)
            {
                return governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
            }
        }

        var localDescriptor = repoRegistryService.List().FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase));
        return localDescriptor is null
            ? governanceService.ResolveRepoPolicy("balanced")
            : governanceService.ResolveRepoPolicy(localDescriptor.PolicyProfile);
    }

    private static string MapCategory(WorkerPermissionKind kind)
    {
        return kind switch
        {
            WorkerPermissionKind.FilesystemWrite => "filesystem_write",
            WorkerPermissionKind.FilesystemDelete => "filesystem_delete",
            WorkerPermissionKind.OutsideWorkspaceAccess => "outside_workspace_access",
            WorkerPermissionKind.NetworkAccess => "network_access",
            WorkerPermissionKind.ProcessControl => "process_control",
            WorkerPermissionKind.SystemConfiguration => "system_configuration",
            WorkerPermissionKind.SecretAccess => "secret_access",
            WorkerPermissionKind.ElevatedPrivilege => "elevated_privilege",
            _ => "unknown_permission_request",
        };
    }

    private static WorkerPermissionPolicyDecision Allow(string code, string reason, string consequence)
    {
        return new WorkerPermissionPolicyDecision
        {
            Decision = WorkerPermissionDecision.Allow,
            ReasonCode = code,
            Reason = reason,
            ConsequenceSummary = consequence,
        };
    }

    private static WorkerPermissionPolicyDecision Deny(string code, string reason, string consequence)
    {
        return new WorkerPermissionPolicyDecision
        {
            Decision = WorkerPermissionDecision.Deny,
            ReasonCode = code,
            Reason = reason,
            ConsequenceSummary = consequence,
        };
    }

    private static WorkerPermissionPolicyDecision Review(string code, string reason, string consequence)
    {
        return new WorkerPermissionPolicyDecision
        {
            Decision = WorkerPermissionDecision.Review,
            ReasonCode = code,
            Reason = reason,
            ConsequenceSummary = consequence,
        };
    }

    private static ApprovalRuntimePolicy ToRuntimeApprovalPolicy(WorkerApprovalOperationalPolicy policy)
    {
        return new ApprovalRuntimePolicy(
            Version: "1.0",
            OutsideWorkspaceRequiresReview: policy.OutsideWorkspaceRequiresReview,
            HighRiskRequiresReview: policy.HighRiskRequiresReview,
            ManualApprovalModeRequiresReview: policy.ManualApprovalModeRequiresReview,
            AutoAllowCategories: policy.AutoAllowCategories.ToArray(),
            AutoDenyCategories: policy.AutoDenyCategories.ToArray(),
            ForceReviewCategories: policy.ForceReviewCategories.ToArray());
    }
}
