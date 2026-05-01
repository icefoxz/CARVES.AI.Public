using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Configuration;

public sealed record RuntimePolicyBundle(
    DelegationRuntimePolicy Delegation,
    ApprovalRuntimePolicy Approval,
    RoleGovernanceRuntimePolicy RoleGovernance,
    WorkerSelectionRuntimePolicy WorkerSelection,
    TrustProfilesRuntimePolicy TrustProfiles,
    HostInvokeRuntimePolicy HostInvoke,
    GovernanceContinuationGateRuntimePolicy GovernanceContinuationGate);

public sealed record DelegationRuntimePolicy(
    string Version,
    bool RequireInspectBeforeExecution,
    bool RequireResidentHost,
    bool AllowManualExecutionFallback,
    IReadOnlyList<string> InspectCommands,
    IReadOnlyList<string> RunCommands);

public sealed record ApprovalRuntimePolicy(
    string Version,
    bool OutsideWorkspaceRequiresReview,
    bool HighRiskRequiresReview,
    bool ManualApprovalModeRequiresReview,
    IReadOnlyList<string> AutoAllowCategories,
    IReadOnlyList<string> AutoDenyCategories,
    IReadOnlyList<string> ForceReviewCategories);

public sealed record RoleGovernanceRuntimePolicy(
    string Version,
    bool ControlledModeDefault,
    bool ProducerCannotSelfApprove,
    bool ReviewerCannotApproveSameTask,
    Carves.Runtime.Domain.Tasks.TaskRoleBinding DefaultRoleBinding,
    IReadOnlyList<string> ValidationLabFollowOnLanes,
    string RoleMode = "disabled",
    bool PlannerWorkerSplitEnabled = false,
    bool WorkerDelegationEnabled = false,
    bool SchedulerAutoDispatchEnabled = false)
{
    public const string DisabledMode = "disabled";
    public const string AdvisoryMode = "advisory";
    public const string EnabledMode = "enabled";

    public static RoleGovernanceRuntimePolicy CreateDefault()
    {
        return new RoleGovernanceRuntimePolicy(
            Version: "1.0",
            ControlledModeDefault: false,
            ProducerCannotSelfApprove: true,
            ReviewerCannotApproveSameTask: true,
            DefaultRoleBinding: new Carves.Runtime.Domain.Tasks.TaskRoleBinding
            {
                Producer = "planner",
                Executor = "worker",
                Reviewer = "planner",
                Approver = "operator",
                ScopeSteward = "operator",
                PolicyOwner = "operator",
            },
            ValidationLabFollowOnLanes: ["approval_recovery", "controlled_mode_governance"],
            RoleMode: DisabledMode,
            PlannerWorkerSplitEnabled: false,
            WorkerDelegationEnabled: false,
            SchedulerAutoDispatchEnabled: false);
    }
}

public sealed record WorkerSelectionRuntimePolicy(
    string Version,
    string? PreferredBackendId,
    string DefaultTrustProfileId,
    bool AllowRoutingFallback,
    IReadOnlyList<string> FallbackBackendIds,
    IReadOnlyList<string>? AllowedBackendIds);

public sealed record TrustProfilesRuntimePolicy(
    string Version,
    string DefaultProfileId,
    IReadOnlyList<WorkerExecutionProfile> Profiles);

public sealed record HostInvokeRuntimePolicy(
    string Version,
    HostInvokeClassRuntimePolicy DefaultRead,
    HostInvokeClassRuntimePolicy ControlPlaneMutation,
    HostInvokeClassRuntimePolicy AttachFlow,
    HostInvokeClassRuntimePolicy DelegatedExecution);

public sealed record HostInvokeClassRuntimePolicy(
    int RequestTimeoutSeconds,
    bool UseAcceptedOperationPolling,
    int PollIntervalMs,
    int BaseWaitSeconds,
    int StallTimeoutSeconds,
    int MaxWaitSeconds);

public sealed record GovernanceContinuationGateRuntimePolicy(
    string Version,
    bool HoldContinuationWithoutQualifyingDelta,
    IReadOnlyList<string> AcceptedResidualConcentrationFamilies,
    IReadOnlyList<string> ClosureBlockingBacklogKinds);

public sealed record RuntimePolicyValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
