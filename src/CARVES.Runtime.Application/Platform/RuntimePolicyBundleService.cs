using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePolicyBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly HashSet<string> KnownPermissionCategories =
    [
        "filesystem_read",
        "filesystem_write",
        "filesystem_delete",
        "outside_workspace_access",
        "network_access",
        "process_control",
        "system_configuration",
        "secret_access",
        "elevated_privilege",
        "unknown_permission_request",
    ];

    private readonly ControlPlanePaths paths;
    private readonly PlatformGovernanceService governanceService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;
    private readonly ProviderRegistryService providerRegistryService;
    private readonly RuntimeHostInvokePolicyService hostInvokePolicyService;
    private readonly RuntimeGovernanceContinuationGatePolicyService governanceContinuationGatePolicyService;

    public RuntimePolicyBundleService(
        ControlPlanePaths paths,
        PlatformGovernanceService governanceService,
        WorkerOperationalPolicyService operationalPolicyService,
        ProviderRegistryService providerRegistryService)
    {
        this.paths = paths;
        this.governanceService = governanceService;
        this.operationalPolicyService = operationalPolicyService;
        this.providerRegistryService = providerRegistryService;
        hostInvokePolicyService = new RuntimeHostInvokePolicyService(paths);
        governanceContinuationGatePolicyService = new RuntimeGovernanceContinuationGatePolicyService(paths);
        EnsurePersistedDefaults();
    }

    public RuntimePolicyBundle Load()
    {
        return LoadInternal().Bundle;
    }

    public DelegationRuntimePolicy LoadDelegationPolicy()
    {
        return Load().Delegation;
    }

    public ApprovalRuntimePolicy LoadApprovalPolicy()
    {
        return Load().Approval;
    }

    public RoleGovernanceRuntimePolicy LoadRoleGovernancePolicy()
    {
        return Load().RoleGovernance;
    }

    public WorkerSelectionRuntimePolicy LoadWorkerSelectionPolicy()
    {
        return Load().WorkerSelection;
    }

    public WorkerSelectionPolicyActivationResult ActivateExternalCodexWorkerOnly(bool dryRun, string? reason)
    {
        return ActivateExternalAppCliWorkers(
            dryRun,
            reason,
            schemaVersion: "worker-selection-external-codex-activation.v1",
            outcomePrefix: "external_codex_worker",
            operationLabel: "external Codex worker");
    }

    public WorkerSelectionPolicyActivationResult ActivateExternalAppCliWorkers(bool dryRun, string? reason)
    {
        return ActivateExternalAppCliWorkers(
            dryRun,
            reason,
            schemaVersion: "worker-selection-external-app-cli-activation.v1",
            outcomePrefix: "external_app_cli_worker",
            operationLabel: "external app/CLI worker");
    }

    private WorkerSelectionPolicyActivationResult ActivateExternalAppCliWorkers(
        bool dryRun,
        string? reason,
        string schemaVersion,
        string outcomePrefix,
        string operationLabel)
    {
        var previousPolicy = LoadWorkerSelectionPolicy();
        var registeredBackends = providerRegistryService.ListWorkerBackends()
            .Select(backend => backend.BackendId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(backendId => backendId, StringComparer.Ordinal)
            .ToArray();
        var registeredBackendSet = registeredBackends.ToHashSet(StringComparer.Ordinal);
        var forbiddenBackendIds = registeredBackends
            .Where(IsSdkOrApiWorkerBackend)
            .OrderBy(backendId => backendId, StringComparer.Ordinal)
            .ToArray();
        var proposedAllowedBackendIds = new[] { "codex_cli", "null_worker" };
        var forbiddenBackendIdsPresent = proposedAllowedBackendIds
            .Where(backendId => forbiddenBackendIds.Contains(backendId, StringComparer.Ordinal))
            .ToArray();
        var codexCliRegistered = registeredBackendSet.Contains("codex_cli");
        var blockedReason = !codexCliRegistered
            ? "codex_cli_not_registered"
            : forbiddenBackendIdsPresent.Length > 0
                ? "forbidden_sdk_api_backend_present"
                : string.Empty;
        var proposedPolicy = previousPolicy with
        {
            PreferredBackendId = "codex_cli",
            AllowRoutingFallback = true,
            FallbackBackendIds = ["null_worker"],
            AllowedBackendIds = proposedAllowedBackendIds,
        };
        var applied = !dryRun && string.IsNullOrWhiteSpace(blockedReason);
        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? $"Activated {operationLabel} policy: current materialized execution admits Codex CLI, SDK/API worker backends stay closed, and future external app/CLI adapters require governed onboarding."
            : reason.Trim();

        if (applied)
        {
            WritePolicyFile(paths.PlatformWorkerSelectionPolicyFile, proposedPolicy);
            governanceService.RecordEvent(
                GovernanceEventType.WorkerTrustedProfileActivated,
                string.Empty,
                resolvedReason);
        }

        return new WorkerSelectionPolicyActivationResult(
            schemaVersion,
            dryRun,
            applied,
            string.IsNullOrWhiteSpace(blockedReason),
            dryRun
                ? $"{outcomePrefix}_activation_dry_run"
                : applied
                    ? $"{outcomePrefix}_policy_activated"
                    : $"{outcomePrefix}_activation_blocked",
            string.IsNullOrWhiteSpace(blockedReason) ? null : blockedReason,
            "external_app_cli_only",
            paths.PlatformWorkerSelectionPolicyFile,
            previousPolicy,
            proposedPolicy,
            [
                "codex_app_schedule_evidence_callback",
                "codex_cli_host_routed_execution",
                "external_agent_app_cli_adapter_governed_onboarding",
                "null_worker_diagnostic_fallback"
            ],
            proposedAllowedBackendIds,
            ["codex_sdk", "openai_api", "claude_api", "gemini_api"],
            forbiddenBackendIds,
            forbiddenBackendIdsPresent,
            codexCliRegistered,
            proposedAllowedBackendIds.Contains("codex_cli", StringComparer.Ordinal),
            proposedAllowedBackendIds.Contains("codex_sdk", StringComparer.Ordinal),
            !proposedAllowedBackendIds.Any(IsSdkOrApiWorkerBackend),
            false,
            false,
            false,
            false,
            resolvedReason,
            DateTimeOffset.UtcNow,
            CurrentActivationMode: "external_app_cli_only",
            CurrentMaterializedWorkerBackendId: "codex_cli",
            ProviderNeutralExternalAppCliPolicy: true,
            FutureExternalAppCliAdaptersRequireGovernedOnboarding: true,
            SdkApiWorkerBoundary: "closed_until_separate_governed_activation");
    }

    public TrustProfilesRuntimePolicy LoadTrustProfilesPolicy()
    {
        return Load().TrustProfiles;
    }

    public HostInvokeRuntimePolicy LoadHostInvokePolicy()
    {
        return Load().HostInvoke;
    }

    public GovernanceContinuationGateRuntimePolicy LoadGovernanceContinuationGatePolicy()
    {
        return Load().GovernanceContinuationGate;
    }

    public RuntimePolicyValidationResult Validate()
    {
        var result = LoadInternal();
        return new RuntimePolicyValidationResult(
            result.Errors.Count == 0,
            result.Errors,
            result.Warnings);
    }

    public IReadOnlyList<string> Describe()
    {
        var bundle = Load();
        var validation = Validate();
        return
        [
            $"Delegation policy: {Path.GetFileName(paths.PlatformDelegationPolicyFile)}; inspect_required={bundle.Delegation.RequireInspectBeforeExecution}; host_required={bundle.Delegation.RequireResidentHost}; manual_fallback={bundle.Delegation.AllowManualExecutionFallback}",
            $"Approval policy: {Path.GetFileName(paths.PlatformApprovalPolicyFile)}; allow/review/deny={bundle.Approval.AutoAllowCategories.Count}/{bundle.Approval.ForceReviewCategories.Count}/{bundle.Approval.AutoDenyCategories.Count}",
            $"Role governance policy: {Path.GetFileName(paths.PlatformRoleGovernancePolicyFile)}; role_mode={bundle.RoleGovernance.RoleMode}; controlled_mode_default={bundle.RoleGovernance.ControlledModeDefault}; planner_worker_split_enabled={bundle.RoleGovernance.PlannerWorkerSplitEnabled}; worker_delegation_enabled={bundle.RoleGovernance.WorkerDelegationEnabled}; scheduler_auto_dispatch_enabled={bundle.RoleGovernance.SchedulerAutoDispatchEnabled}; producer_cannot_self_approve={bundle.RoleGovernance.ProducerCannotSelfApprove}; reviewer_cannot_approve_same_task={bundle.RoleGovernance.ReviewerCannotApproveSameTask}",
            $"Worker selection policy: {Path.GetFileName(paths.PlatformWorkerSelectionPolicyFile)}; preferred_backend={bundle.WorkerSelection.PreferredBackendId ?? "(none)"}; default_trust={bundle.WorkerSelection.DefaultTrustProfileId}; fallback_backends={bundle.WorkerSelection.FallbackBackendIds.Count}; allowed_backends={(bundle.WorkerSelection.AllowedBackendIds?.Count ?? 0)}",
            $"Trust profiles: {Path.GetFileName(paths.PlatformTrustProfilesFile)}; default={bundle.TrustProfiles.DefaultProfileId}; profiles={bundle.TrustProfiles.Profiles.Count}",
            $"Host invoke policy: {Path.GetFileName(paths.PlatformHostInvokePolicyFile)}; control_plane_mutation=request_timeout={bundle.HostInvoke.ControlPlaneMutation.RequestTimeoutSeconds}s; accepted_polling={bundle.HostInvoke.ControlPlaneMutation.UseAcceptedOperationPolling}; base_wait={bundle.HostInvoke.ControlPlaneMutation.BaseWaitSeconds}s; hard_max_wait={bundle.HostInvoke.ControlPlaneMutation.MaxWaitSeconds}s",
            $"Governance continuation gate policy: {Path.GetFileName(paths.PlatformGovernanceContinuationGatePolicyFile)}; hold_without_delta={bundle.GovernanceContinuationGate.HoldContinuationWithoutQualifyingDelta}; accepted_residual_families={bundle.GovernanceContinuationGate.AcceptedResidualConcentrationFamilies.Count}; closure_blocking_kinds={bundle.GovernanceContinuationGate.ClosureBlockingBacklogKinds.Count}",
            $"Policy validation: valid={validation.IsValid}; errors={validation.Errors.Count}; warnings={validation.Warnings.Count}",
        ];
    }

    public void EnsurePersistedDefaults()
    {
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        var defaults = BuildDefaults();
        EnsureFile(paths.PlatformDelegationPolicyFile, defaults.Delegation);
        EnsureFile(paths.PlatformApprovalPolicyFile, defaults.Approval);
        EnsureFile(paths.PlatformRoleGovernancePolicyFile, defaults.RoleGovernance);
        EnsureFile(paths.PlatformWorkerSelectionPolicyFile, defaults.WorkerSelection);
        EnsureFile(paths.PlatformTrustProfilesFile, defaults.TrustProfiles);
        EnsureFile(paths.PlatformHostInvokePolicyFile, defaults.HostInvoke);
        EnsureFile(paths.PlatformGovernanceContinuationGatePolicyFile, defaults.GovernanceContinuationGate);
    }

    private RuntimePolicyBundleLoadResult LoadInternal()
    {
        EnsurePersistedDefaults();
        var defaults = BuildDefaults();
        var errors = new List<string>();
        var warnings = new List<string>();

        var delegation = LoadDelegation(defaults.Delegation, errors, warnings);
        var approval = LoadApproval(defaults.Approval, errors, warnings);
        var roleGovernance = LoadRoleGovernance(defaults.RoleGovernance, errors, warnings);
        var workerSelection = LoadWorkerSelection(defaults.WorkerSelection, defaults.TrustProfiles, errors, warnings);
        var trustProfiles = LoadTrustProfiles(defaults.TrustProfiles, errors, warnings);
        var hostInvoke = hostInvokePolicyService.LoadPolicy();
        var hostInvokeValidation = hostInvokePolicyService.Validate();
        errors.AddRange(hostInvokeValidation.Errors);
        warnings.AddRange(hostInvokeValidation.Warnings);
        var governanceContinuationGate = governanceContinuationGatePolicyService.LoadPolicy();
        var governanceContinuationGateValidation = governanceContinuationGatePolicyService.Validate();
        errors.AddRange(governanceContinuationGateValidation.Errors);
        warnings.AddRange(governanceContinuationGateValidation.Warnings);
        if (!trustProfiles.Profiles.Any(profile => string.Equals(profile.ProfileId, workerSelection.DefaultTrustProfileId, StringComparison.Ordinal)))
        {
            warnings.Add($"Worker selection default trust profile '{workerSelection.DefaultTrustProfileId}' is missing from trust profiles; using '{trustProfiles.DefaultProfileId}'.");
            workerSelection = workerSelection with { DefaultTrustProfileId = trustProfiles.DefaultProfileId };
        }

        return new RuntimePolicyBundleLoadResult(
            new RuntimePolicyBundle(delegation, approval, roleGovernance, workerSelection, trustProfiles, hostInvoke, governanceContinuationGate),
            errors,
            warnings);
    }

    private RuntimePolicyBundle BuildDefaults()
    {
        var operationalPolicy = operationalPolicyService.GetPolicy();
        var governance = governanceService.GetSnapshot();
        var repoPolicy = governance.RepoPolicies.FirstOrDefault(policy => string.Equals(policy.ProfileId, "balanced", StringComparison.Ordinal))
            ?? governance.RepoPolicies.FirstOrDefault()
            ?? throw new InvalidOperationException("Platform governance does not define any repo policy.");
        var trustProfiles = governance.WorkerPolicies
            .SelectMany(policy => policy.Profiles)
            .GroupBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .ToArray();
        var defaultTrustProfileId = !string.IsNullOrWhiteSpace(operationalPolicy.PreferredTrustProfileId)
            ? operationalPolicy.PreferredTrustProfileId
            : !string.IsNullOrWhiteSpace(repoPolicy.PreferredTrustProfileId)
                ? repoPolicy.PreferredTrustProfileId
                : trustProfiles.FirstOrDefault()?.ProfileId ?? WorkerExecutionProfile.UntrustedDefault.ProfileId;
        var backends = providerRegistryService.ListWorkerBackends()
            .Select(backend => backend.BackendId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(backendId => backendId, StringComparer.Ordinal)
            .ToArray();
        var fallbackBackends = new[] { "null_worker" };
        var allowedBackends = new[] { "null_worker" };

        return new RuntimePolicyBundle(
            new DelegationRuntimePolicy(
                Version: "1.0",
                RequireInspectBeforeExecution: true,
                RequireResidentHost: true,
                AllowManualExecutionFallback: false,
                InspectCommands: ["task inspect <task-id>", "card inspect <card-id>"],
                RunCommands: ["task run <task-id>"]),
            new ApprovalRuntimePolicy(
                Version: "1.0",
                OutsideWorkspaceRequiresReview: operationalPolicy.Approval.OutsideWorkspaceRequiresReview,
                HighRiskRequiresReview: operationalPolicy.Approval.HighRiskRequiresReview,
                ManualApprovalModeRequiresReview: operationalPolicy.Approval.ManualApprovalModeRequiresReview,
                AutoAllowCategories: operationalPolicy.Approval.AutoAllowCategories.ToArray(),
                AutoDenyCategories: operationalPolicy.Approval.AutoDenyCategories.ToArray(),
                ForceReviewCategories: operationalPolicy.Approval.ForceReviewCategories.ToArray()),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new WorkerSelectionRuntimePolicy(
                Version: "1.0",
                PreferredBackendId: "null_worker",
                DefaultTrustProfileId: "workspace_build_test",
                AllowRoutingFallback: true,
                FallbackBackendIds: fallbackBackends,
                AllowedBackendIds: allowedBackends),
            new TrustProfilesRuntimePolicy(
                Version: "1.0",
                DefaultProfileId: defaultTrustProfileId,
                Profiles: trustProfiles),
            hostInvokePolicyService.LoadPolicy(),
            governanceContinuationGatePolicyService.LoadPolicy());
    }

    private DelegationRuntimePolicy LoadDelegation(
        DelegationRuntimePolicy defaults,
        List<string> errors,
        List<string> warnings)
    {
        var policy = LoadFile(paths.PlatformDelegationPolicyFile, defaults, errors);
        if (policy.InspectCommands.Count == 0)
        {
            errors.Add("Delegation policy requires at least one inspect command.");
            return defaults;
        }

        if (policy.RunCommands.Count == 0)
        {
            errors.Add("Delegation policy requires at least one run command.");
            return defaults;
        }

        if (!policy.RequireResidentHost && !policy.AllowManualExecutionFallback)
        {
            warnings.Add("Delegation policy disables resident-host requirement while also disallowing manual fallback.");
        }

        return policy;
    }

    private RoleGovernanceRuntimePolicy LoadRoleGovernance(
        RoleGovernanceRuntimePolicy defaults,
        List<string> errors,
        List<string> warnings)
    {
        var policy = LoadFile(paths.PlatformRoleGovernancePolicyFile, defaults, errors);
        var roleMode = NormalizeRoleMode(policy.RoleMode);
        if (roleMode is null)
        {
            errors.Add($"Role governance policy role_mode must be one of: {RoleGovernanceRuntimePolicy.DisabledMode}, {RoleGovernanceRuntimePolicy.AdvisoryMode}, {RoleGovernanceRuntimePolicy.EnabledMode}.");
            return defaults;
        }

        policy = policy with { RoleMode = roleMode };
        if (!string.Equals(policy.RoleMode, RoleGovernanceRuntimePolicy.EnabledMode, StringComparison.Ordinal))
        {
            if (policy.ControlledModeDefault
                || policy.PlannerWorkerSplitEnabled
                || policy.WorkerDelegationEnabled
                || policy.SchedulerAutoDispatchEnabled)
            {
                warnings.Add("Role governance policy is not enabled; controlled mode, planner/worker split, worker delegation, and scheduler auto-dispatch are held disabled.");
            }

            policy = policy with
            {
                ControlledModeDefault = false,
                PlannerWorkerSplitEnabled = false,
                WorkerDelegationEnabled = false,
                SchedulerAutoDispatchEnabled = false,
            };
        }

        if (string.IsNullOrWhiteSpace(policy.DefaultRoleBinding.Producer)
            || string.IsNullOrWhiteSpace(policy.DefaultRoleBinding.Reviewer)
            || string.IsNullOrWhiteSpace(policy.DefaultRoleBinding.Approver)
            || string.IsNullOrWhiteSpace(policy.DefaultRoleBinding.ScopeSteward)
            || string.IsNullOrWhiteSpace(policy.DefaultRoleBinding.PolicyOwner))
        {
            errors.Add("Role governance policy requires non-empty producer, reviewer, approver, scope_steward, and policy_owner defaults.");
            return defaults;
        }

        if (policy.ProducerCannotSelfApprove
            && string.Equals(policy.DefaultRoleBinding.Producer, policy.DefaultRoleBinding.Approver, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Role governance policy default role binding violates producer_cannot_self_approve.");
            return defaults;
        }

        if (policy.ReviewerCannotApproveSameTask
            && string.Equals(policy.DefaultRoleBinding.Reviewer, policy.DefaultRoleBinding.Approver, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Role governance policy default role binding violates reviewer_cannot_approve_same_task.");
            return defaults;
        }

        if (policy.ValidationLabFollowOnLanes.Count == 0)
        {
            warnings.Add("Role governance policy does not declare any ValidationLab follow-on lanes.");
        }

        return policy;
    }

    private static string? NormalizeRoleMode(string? roleMode)
    {
        var normalized = string.IsNullOrWhiteSpace(roleMode)
            ? RoleGovernanceRuntimePolicy.DisabledMode
            : roleMode.Trim().ToLowerInvariant();

        return normalized switch
        {
            RoleGovernanceRuntimePolicy.DisabledMode => RoleGovernanceRuntimePolicy.DisabledMode,
            RoleGovernanceRuntimePolicy.AdvisoryMode => RoleGovernanceRuntimePolicy.AdvisoryMode,
            RoleGovernanceRuntimePolicy.EnabledMode => RoleGovernanceRuntimePolicy.EnabledMode,
            _ => null,
        };
    }

    private ApprovalRuntimePolicy LoadApproval(
        ApprovalRuntimePolicy defaults,
        List<string> errors,
        List<string> warnings)
    {
        var policy = LoadFile(paths.PlatformApprovalPolicyFile, defaults, errors);
        var invalidCategories = policy.AutoAllowCategories
            .Concat(policy.AutoDenyCategories)
            .Concat(policy.ForceReviewCategories)
            .Where(category => !KnownPermissionCategories.Contains(category))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (invalidCategories.Length > 0)
        {
            errors.Add($"Approval policy contains unknown permission categories: {string.Join(", ", invalidCategories)}.");
            return defaults;
        }

        foreach (var overlapping in policy.AutoAllowCategories.Intersect(policy.AutoDenyCategories, StringComparer.Ordinal))
        {
            warnings.Add($"Approval policy category '{overlapping}' is present in both auto-allow and auto-deny.");
        }

        return policy;
    }

    private WorkerSelectionRuntimePolicy LoadWorkerSelection(
        WorkerSelectionRuntimePolicy defaults,
        TrustProfilesRuntimePolicy trustProfiles,
        List<string> errors,
        List<string> warnings)
    {
        var policy = LoadFile(paths.PlatformWorkerSelectionPolicyFile, defaults, errors);
        if (string.IsNullOrWhiteSpace(policy.DefaultTrustProfileId))
        {
            errors.Add("Worker selection policy requires a default trust profile id.");
            return defaults;
        }

        if (!trustProfiles.Profiles.Any(profile => string.Equals(profile.ProfileId, policy.DefaultTrustProfileId, StringComparison.Ordinal)))
        {
            warnings.Add($"Worker selection default trust profile '{policy.DefaultTrustProfileId}' is not defined in trust-profiles.json.");
        }

        var availableBackends = providerRegistryService.ListWorkerBackends()
            .Select(backend => backend.BackendId)
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(policy.PreferredBackendId) && !availableBackends.Contains(policy.PreferredBackendId))
        {
            warnings.Add($"Preferred backend '{policy.PreferredBackendId}' is not currently registered.");
        }

        foreach (var backendId in policy.FallbackBackendIds.Where(backendId => !availableBackends.Contains(backendId)).Distinct(StringComparer.Ordinal))
        {
            warnings.Add($"Fallback backend '{backendId}' is not currently registered.");
        }

        var normalizedAllowedBackendIds = (policy.AllowedBackendIds ?? Array.Empty<string>())
            .Where(backendId => !string.IsNullOrWhiteSpace(backendId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedAllowedBackendIds.Length == 0)
        {
            errors.Add("Worker selection policy requires at least one allowed backend id.");
            return defaults;
        }

        foreach (var backendId in normalizedAllowedBackendIds.Where(backendId => !availableBackends.Contains(backendId)))
        {
            warnings.Add($"Allowed backend '{backendId}' is not currently registered.");
        }

        if (!string.IsNullOrWhiteSpace(policy.PreferredBackendId)
            && normalizedAllowedBackendIds.Length > 0
            && !normalizedAllowedBackendIds.Contains(policy.PreferredBackendId, StringComparer.Ordinal))
        {
            warnings.Add($"Preferred backend '{policy.PreferredBackendId}' is not present in allowed_backends.");
        }

        return policy with
        {
            FallbackBackendIds = policy.FallbackBackendIds.Distinct(StringComparer.Ordinal).ToArray(),
            AllowedBackendIds = normalizedAllowedBackendIds,
        };
    }

    private TrustProfilesRuntimePolicy LoadTrustProfiles(
        TrustProfilesRuntimePolicy defaults,
        List<string> errors,
        List<string> warnings)
    {
        var policy = LoadFile(paths.PlatformTrustProfilesFile, defaults, errors);
        if (policy.Profiles.Count == 0)
        {
            errors.Add("Trust profiles policy requires at least one profile.");
            return defaults;
        }

        var duplicateIds = policy.Profiles
            .GroupBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            errors.Add($"Trust profiles policy contains duplicate profile ids: {string.Join(", ", duplicateIds)}.");
            return defaults;
        }

        if (!policy.Profiles.Any(profile => string.Equals(profile.ProfileId, policy.DefaultProfileId, StringComparison.Ordinal)))
        {
            errors.Add($"Trust profiles default profile '{policy.DefaultProfileId}' is not defined.");
            return defaults;
        }

        foreach (var profile in policy.Profiles.Where(profile => profile.AllowedPermissionCategories.Count == 0))
        {
            warnings.Add($"Trust profile '{profile.ProfileId}' allows no permission categories.");
        }

        return policy with
        {
            Profiles = policy.Profiles.OrderBy(profile => profile.ProfileId, StringComparer.Ordinal).ToArray(),
        };
    }

    private static void EnsureFile<T>(string path, T value)
    {
        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void WritePolicyFile<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
    }

    private static bool IsSdkOrApiWorkerBackend(string backendId)
    {
        return backendId.EndsWith("_api", StringComparison.Ordinal)
               || backendId.EndsWith("_sdk", StringComparison.Ordinal)
               || string.Equals(backendId, "codex_sdk", StringComparison.Ordinal);
    }

    private T LoadFile<T>(string path, T defaults, List<string> errors)
    {
        try
        {
            if (!File.Exists(path))
            {
                return defaults;
            }

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (parsed is null)
            {
                errors.Add($"Policy file '{Path.GetFileName(path)}' is empty or invalid; safe defaults are in use.");
                return defaults;
            }

            return parsed;
        }
        catch (Exception exception)
        {
            errors.Add($"Policy file '{Path.GetFileName(path)}' could not be loaded: {exception.Message}. Safe defaults are in use.");
            return defaults;
        }
    }

    private sealed record RuntimePolicyBundleLoadResult(
        RuntimePolicyBundle Bundle,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);
}

public sealed record WorkerSelectionPolicyActivationResult(
    string SchemaVersion,
    bool DryRun,
    bool Applied,
    bool Allowed,
    string Outcome,
    string? BlockedReason,
    string ActivationMode,
    string PolicyFile,
    WorkerSelectionRuntimePolicy PreviousPolicy,
    WorkerSelectionRuntimePolicy ProposedPolicy,
    IReadOnlyList<string> AllowedWorkerPaths,
    IReadOnlyList<string> AllowedBackendIds,
    IReadOnlyList<string> DisallowedBackendIds,
    IReadOnlyList<string> RegisteredForbiddenBackendIds,
    IReadOnlyList<string> ForbiddenBackendIdsPresent,
    bool CodexCliRegistered,
    bool CodexCliAllowed,
    bool CodexSdkAllowed,
    bool SdkApiBackendsClosed,
    bool StartsRun,
    bool IssuesLease,
    bool IngestsResult,
    bool WritesTaskTruth,
    string Reason,
    DateTimeOffset ActivatedAt,
    string CurrentActivationMode = "external_app_cli_only",
    string CurrentMaterializedWorkerBackendId = "codex_cli",
    bool ProviderNeutralExternalAppCliPolicy = true,
    bool FutureExternalAppCliAdaptersRequireGovernedOnboarding = true,
    string SdkApiWorkerBoundary = "closed_until_separate_governed_activation");
