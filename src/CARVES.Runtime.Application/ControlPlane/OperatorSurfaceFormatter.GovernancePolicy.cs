using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult Governance(PlatformGovernanceSnapshot snapshot, IReadOnlyList<GovernanceEvent> events)
    {
        return OperatorCommandResult.Success(
            $"Platform policy: {snapshot.PlatformPolicy.PolicyId}",
            $"Repo policies: {snapshot.RepoPolicies.Count}",
            $"Provider policies: {snapshot.ProviderPolicies.Count}",
            $"Worker policies: {snapshot.WorkerPolicies.Count}",
            $"Review policies: {snapshot.ReviewPolicies.Count}",
            $"Governance events: {events.Count}");
    }

    public static OperatorCommandResult GovernanceInspect(
        RepoDescriptor descriptor,
        RepoPolicy repoPolicy,
        ProviderPolicy providerPolicy,
        WorkerPolicy workerPolicy,
        ReviewPolicy reviewPolicy)
    {
        return OperatorCommandResult.Success(
            $"Repo governance: {descriptor.RepoId}",
            $"Repo policy: {repoPolicy.ProfileId}",
            $"Planner/task caps: rounds={repoPolicy.MaxPlannerRounds}; generated={repoPolicy.MaxGeneratedTasks}; concurrent={repoPolicy.MaxConcurrentExecutions}",
            $"Provider policy: {providerPolicy.PolicyId}",
            $"Worker policy: {workerPolicy.PolicyId}",
            $"Review policy: {reviewPolicy.PolicyId}",
            $"Manual approval mode: {repoPolicy.ManualApprovalMode}");
    }

    public static OperatorCommandResult PolicyInspect(
        ControlPlanePaths paths,
        RuntimePolicyBundle bundle,
        RuntimePolicyValidationResult validation,
        RuntimeExportProfilesSurface exportProfiles)
    {
        var lines = new List<string>
        {
            "Externalized runtime policies:",
            $"Delegation file: {paths.PlatformDelegationPolicyFile}",
            $"- require inspect before execution: {bundle.Delegation.RequireInspectBeforeExecution}",
            $"- require resident host: {bundle.Delegation.RequireResidentHost}",
            $"- allow manual execution fallback: {bundle.Delegation.AllowManualExecutionFallback}",
            $"- inspect commands: {(bundle.Delegation.InspectCommands.Count == 0 ? "(none)" : string.Join(", ", bundle.Delegation.InspectCommands))}",
            $"- run commands: {(bundle.Delegation.RunCommands.Count == 0 ? "(none)" : string.Join(", ", bundle.Delegation.RunCommands))}",
            $"Approval file: {paths.PlatformApprovalPolicyFile}",
            $"- outside workspace requires review: {bundle.Approval.OutsideWorkspaceRequiresReview}",
            $"- high risk requires review: {bundle.Approval.HighRiskRequiresReview}",
            $"- manual approval mode requires review: {bundle.Approval.ManualApprovalModeRequiresReview}",
            $"- auto allow: {(bundle.Approval.AutoAllowCategories.Count == 0 ? "(none)" : string.Join(", ", bundle.Approval.AutoAllowCategories))}",
            $"- auto deny: {(bundle.Approval.AutoDenyCategories.Count == 0 ? "(none)" : string.Join(", ", bundle.Approval.AutoDenyCategories))}",
            $"- force review: {(bundle.Approval.ForceReviewCategories.Count == 0 ? "(none)" : string.Join(", ", bundle.Approval.ForceReviewCategories))}",
            $"Role governance file: {paths.PlatformRoleGovernancePolicyFile}",
            $"- role mode: {bundle.RoleGovernance.RoleMode}",
            $"- controlled mode default: {bundle.RoleGovernance.ControlledModeDefault}",
            $"- planner/worker split enabled: {bundle.RoleGovernance.PlannerWorkerSplitEnabled}",
            $"- worker delegation enabled: {bundle.RoleGovernance.WorkerDelegationEnabled}",
            $"- scheduler auto-dispatch enabled: {bundle.RoleGovernance.SchedulerAutoDispatchEnabled}",
            $"- producer cannot self approve: {bundle.RoleGovernance.ProducerCannotSelfApprove}",
            $"- reviewer cannot approve same task: {bundle.RoleGovernance.ReviewerCannotApproveSameTask}",
            $"- default roles: producer={bundle.RoleGovernance.DefaultRoleBinding.Producer}; executor={bundle.RoleGovernance.DefaultRoleBinding.Executor}; reviewer={bundle.RoleGovernance.DefaultRoleBinding.Reviewer}; approver={bundle.RoleGovernance.DefaultRoleBinding.Approver}; scope_steward={bundle.RoleGovernance.DefaultRoleBinding.ScopeSteward}; policy_owner={bundle.RoleGovernance.DefaultRoleBinding.PolicyOwner}",
            $"- validationlab follow-on lanes: {(bundle.RoleGovernance.ValidationLabFollowOnLanes.Count == 0 ? "(none)" : string.Join(", ", bundle.RoleGovernance.ValidationLabFollowOnLanes))}",
            $"Worker selection file: {paths.PlatformWorkerSelectionPolicyFile}",
            $"- preferred backend: {bundle.WorkerSelection.PreferredBackendId ?? "(none)"}",
            $"- default trust profile: {bundle.WorkerSelection.DefaultTrustProfileId}",
            $"- allow routing fallback: {bundle.WorkerSelection.AllowRoutingFallback}",
            $"- fallback backends: {(bundle.WorkerSelection.FallbackBackendIds.Count == 0 ? "(none)" : string.Join(", ", bundle.WorkerSelection.FallbackBackendIds))}",
            $"- allowed backends: {(bundle.WorkerSelection.AllowedBackendIds is null || bundle.WorkerSelection.AllowedBackendIds.Count == 0 ? "(none)" : string.Join(", ", bundle.WorkerSelection.AllowedBackendIds))}",
            $"Trust profiles file: {paths.PlatformTrustProfilesFile}",
            $"- default profile: {bundle.TrustProfiles.DefaultProfileId}",
            $"- profiles: {bundle.TrustProfiles.Profiles.Count}",
            $"Host invoke file: {paths.PlatformHostInvokePolicyFile}",
            $"- default_read: request_timeout={bundle.HostInvoke.DefaultRead.RequestTimeoutSeconds}s; accepted_polling={bundle.HostInvoke.DefaultRead.UseAcceptedOperationPolling}; poll_interval={bundle.HostInvoke.DefaultRead.PollIntervalMs}ms; base_wait={bundle.HostInvoke.DefaultRead.BaseWaitSeconds}s; stall_timeout={bundle.HostInvoke.DefaultRead.StallTimeoutSeconds}s; hard_max_wait={bundle.HostInvoke.DefaultRead.MaxWaitSeconds}s",
            $"- control_plane_mutation: request_timeout={bundle.HostInvoke.ControlPlaneMutation.RequestTimeoutSeconds}s; accepted_polling={bundle.HostInvoke.ControlPlaneMutation.UseAcceptedOperationPolling}; poll_interval={bundle.HostInvoke.ControlPlaneMutation.PollIntervalMs}ms; base_wait={bundle.HostInvoke.ControlPlaneMutation.BaseWaitSeconds}s; stall_timeout={bundle.HostInvoke.ControlPlaneMutation.StallTimeoutSeconds}s; hard_max_wait={bundle.HostInvoke.ControlPlaneMutation.MaxWaitSeconds}s",
            $"- attach_flow: request_timeout={bundle.HostInvoke.AttachFlow.RequestTimeoutSeconds}s; accepted_polling={bundle.HostInvoke.AttachFlow.UseAcceptedOperationPolling}; poll_interval={bundle.HostInvoke.AttachFlow.PollIntervalMs}ms; base_wait={bundle.HostInvoke.AttachFlow.BaseWaitSeconds}s; stall_timeout={bundle.HostInvoke.AttachFlow.StallTimeoutSeconds}s; hard_max_wait={bundle.HostInvoke.AttachFlow.MaxWaitSeconds}s",
            $"- delegated_execution: request_timeout={bundle.HostInvoke.DelegatedExecution.RequestTimeoutSeconds}s; accepted_polling={bundle.HostInvoke.DelegatedExecution.UseAcceptedOperationPolling}; poll_interval={bundle.HostInvoke.DelegatedExecution.PollIntervalMs}ms; base_wait={bundle.HostInvoke.DelegatedExecution.BaseWaitSeconds}s; stall_timeout={bundle.HostInvoke.DelegatedExecution.StallTimeoutSeconds}s; hard_max_wait={bundle.HostInvoke.DelegatedExecution.MaxWaitSeconds}s",
            $"Governance continuation gate file: {paths.PlatformGovernanceContinuationGatePolicyFile}",
            $"- hold_without_delta: {bundle.GovernanceContinuationGate.HoldContinuationWithoutQualifyingDelta}",
            $"- accepted_residual_families: {(bundle.GovernanceContinuationGate.AcceptedResidualConcentrationFamilies.Count == 0 ? "(none)" : string.Join(", ", bundle.GovernanceContinuationGate.AcceptedResidualConcentrationFamilies))}",
            $"- closure_blocking_kinds: {(bundle.GovernanceContinuationGate.ClosureBlockingBacklogKinds.Count == 0 ? "(none)" : string.Join(", ", bundle.GovernanceContinuationGate.ClosureBlockingBacklogKinds))}",
            $"Export profiles file: {exportProfiles.PolicyFile}",
            $"- profiles: {exportProfiles.Profiles.Count}",
            $"- profile ids: {(exportProfiles.Profiles.Count == 0 ? "(none)" : string.Join(", ", exportProfiles.Profiles.Select(profile => profile.ProfileId)))}",
        };

        lines.AddRange(bundle.TrustProfiles.Profiles.Select(profile =>
            $"  - {profile.ProfileId}: trusted={profile.Trusted}; sandbox={profile.SandboxMode}; approval={profile.ApprovalMode}; boundary={profile.WorkspaceBoundary}; network={profile.NetworkAccessEnabled}"));

        lines.Add($"Validation valid: {validation.IsValid}");
        lines.Add($"Validation errors: {validation.Errors.Count}");
        lines.AddRange(validation.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {validation.Warnings.Count}");
        lines.AddRange(validation.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(validation.IsValid ? 0 : 1, lines);
    }

    public static OperatorCommandResult RuntimeExportProfiles(RuntimeExportProfilesSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime export profiles",
            $"Policy file: {surface.PolicyFile}",
            $"Artifact catalog schema: {surface.ArtifactCatalogSchemaVersion}",
            surface.Summary,
            $"Profiles: {surface.Profiles.Count}",
        };

        foreach (var profile in surface.Profiles)
        {
            lines.Add($"- {profile.ProfileId}: {profile.DisplayName}; families={profile.IncludedFamilies.Count}; paths={profile.IncludedPathRoots.Count}; excluded_families={profile.ExcludedFamilies.Count}");
            lines.Add($"  summary: {profile.Summary}");
            lines.Add($"  discipline: valid={profile.Discipline.IsValid}; full={profile.Discipline.FullFamilyCount}; manifest_only={profile.Discipline.ManifestOnlyFamilyCount}; pointer_only={profile.Discipline.PointerOnlyFamilyCount}");

            if (profile.Discipline.FullFamilyIds.Count > 0)
            {
                lines.Add($"  full families: {string.Join(", ", profile.Discipline.FullFamilyIds)}");
            }

            if (profile.Discipline.ManifestOnlyFamilyIds.Count > 0)
            {
                lines.Add($"  manifest_only families: {string.Join(", ", profile.Discipline.ManifestOnlyFamilyIds)}");
            }

            if (profile.Discipline.PointerOnlyFamilyIds.Count > 0)
            {
                lines.Add($"  pointer_only families: {string.Join(", ", profile.Discipline.PointerOnlyFamilyIds)}");
            }

            lines.AddRange(profile.IncludedFamilies.Select(family =>
                $"  include {family.FamilyId}: mode={System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(family.PackagingMode.ToString())}; class={System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(family.ArtifactClass.ToString())}; roots={(family.Roots.Count == 0 ? "(none)" : string.Join(", ", family.Roots))}; reason={family.Reason}"));

            if (profile.IncludedPathRoots.Count > 0)
            {
                lines.Add($"  include paths: {string.Join(", ", profile.IncludedPathRoots)}");
            }

            if (profile.ExcludedFamilies.Count > 0)
            {
                lines.Add($"  exclude families: {string.Join(", ", profile.ExcludedFamilies.Select(family => family.FamilyId))}");
            }

            if (profile.ExcludedPathRoots.Count > 0)
            {
                lines.Add($"  exclude paths: {string.Join(", ", profile.ExcludedPathRoots)}");
            }

            lines.AddRange(profile.Discipline.Errors.Select(error => $"  discipline error: {error}"));
            lines.AddRange(profile.Discipline.Warnings.Select(warning => $"  discipline warning: {warning}"));
            lines.AddRange(profile.Notes.Select(note => $"  note: {note}"));
        }

        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    public static OperatorCommandResult PolicyValidate(RuntimePolicyValidationResult validation)
    {
        var lines = new List<string>
        {
            $"Policy validation: {(validation.IsValid ? "valid" : "invalid")}",
            $"Errors: {validation.Errors.Count}",
        };
        lines.AddRange(validation.Errors.Select(error => $"- {error}"));
        lines.Add($"Warnings: {validation.Warnings.Count}");
        lines.AddRange(validation.Warnings.Select(warning => $"- {warning}"));
        return new OperatorCommandResult(validation.IsValid ? 0 : 1, lines);
    }

    public static OperatorCommandResult GovernanceReport(GovernanceReport report)
    {
        var lines = new List<string>
        {
            $"Governance report window: last {report.WindowHours}h",
            $"Generated at: {report.GeneratedAt:O}",
            $"Recovery success: {report.RecoverySuccessfulCount}/{report.RecoverySampleCount} ({report.RecoverySuccessRate:P0})",
            $"Incompleteness: {report.IncompletenessNote}",
            "Approval decisions:",
        };
        lines.AddRange(report.ApprovalDecisions.Count == 0
            ? ["(none)"]
            : report.ApprovalDecisions.Select(item => $"- {item.Decision}: {item.Count}"));
        lines.Add("Recent approval events:");
        lines.AddRange(report.RecentApprovalEvents.Count == 0
            ? ["(none)"]
            : report.RecentApprovalEvents.Select(item => $"- {item.Decision} request={item.PermissionRequestId} repo={item.RepoId} task={item.TaskId} actor={item.Actor} [{item.OccurredAt:O}]"));
        lines.Add("Unstable providers:");
        lines.AddRange(report.UnstableProviders.Count == 0
            ? ["(none)"]
            : report.UnstableProviders.Select(item => $"- {item.BackendId}: state={item.State}; incidents={item.IncidentCount}; failures={item.ConsecutiveFailureCount}; latency={item.LatencyMs?.ToString() ?? "(none)"}ms; {item.Summary}"));
        lines.Add("Permission-blocked task classes:");
        lines.AddRange(report.PermissionBlockedTaskClasses.Count == 0
            ? ["(none)"]
            : report.PermissionBlockedTaskClasses.Select(item => $"- {item.TaskType}: {item.Count}"));
        lines.Add("Repo incident density:");
        lines.AddRange(report.RepoIncidentDensity.Count == 0
            ? ["(none)"]
            : report.RepoIncidentDensity.Select(item => $"- {item.RepoId}: {item.IncidentCount}"));
        return new OperatorCommandResult(0, lines);
    }
}
