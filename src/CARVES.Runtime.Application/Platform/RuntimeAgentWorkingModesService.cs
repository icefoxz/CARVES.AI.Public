using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentWorkingModesService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly IntentDiscoveryService intentDiscoveryService;
    private readonly FormalPlanningPacketService formalPlanningPacketService;
    private readonly ManagedWorkspaceLeaseService managedWorkspaceLeaseService;

    public RuntimeAgentWorkingModesService(
        string repoRoot,
        IntentDiscoveryService intentDiscoveryService,
        FormalPlanningPacketService formalPlanningPacketService,
        ManagedWorkspaceLeaseService managedWorkspaceLeaseService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.intentDiscoveryService = intentDiscoveryService;
        this.formalPlanningPacketService = formalPlanningPacketService;
        this.managedWorkspaceLeaseService = managedWorkspaceLeaseService;
    }

    public RuntimeAgentWorkingModesSurface Build()
    {
        var errors = new List<string>();
        const string workingModesDocumentPath = "docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md";
        const string collaborationPlaneDocumentPath = "docs/runtime/runtime-constraint-ladder-and-collaboration-plane.md";
        const string cliFirstDocumentPath = "docs/runtime/runtime-cli-first-architecture.md";
        const string implementationPlanPath = "docs/runtime/runtime-agent-working-modes-implementation-plan.md";
        const string collaborationProjectionDocumentPath = "docs/runtime/runtime-collaboration-and-surface-projection.md";
        const string modeEBrokeredExecutionDocumentPath = "docs/runtime/runtime-mode-e-brokered-execution.md";

        ValidatePath(workingModesDocumentPath, "Working modes doctrine document", errors);
        ValidatePath(collaborationPlaneDocumentPath, "Constraint ladder and collaboration-plane document", errors);
        ValidatePath(cliFirstDocumentPath, "CLI-first architecture document", errors);
        ValidatePath(implementationPlanPath, "Working modes implementation plan", errors);
        ValidatePath(collaborationProjectionDocumentPath, "Collaboration and surface projection document", errors);
        ValidatePath(modeEBrokeredExecutionDocumentPath, "Mode E brokered execution document", errors);

        var status = intentDiscoveryService.GetStatus();
        var packet = formalPlanningPacketService.TryBuildCurrentPacket();
        var workspaceSurface = managedWorkspaceLeaseService.BuildSurface();
        var currentMode = RuntimeWorkingModesSemantics.ResolveCurrentMode(workspaceSurface);
        var strongestMode = RuntimeWorkingModesSemantics.ResolveStrongestRuntimeSupportedMode(workspaceSurface);
        var planningCouplingPosture = RuntimeWorkingModesSemantics.ResolvePlanningCouplingPosture(status, packet, workspaceSurface);
        var modeEActivation = ResolveModeEOperationalActivation(packet, errors);
        var modeEActivationBlockingChecks = modeEActivation.Checks.Where(IsBlockingActivationCheck).ToArray();
        var firstModeEActivationBlockingCheck = modeEActivationBlockingChecks.FirstOrDefault();
        var firstModeEActivationPlaybookStep = modeEActivation.PlaybookSteps.FirstOrDefault();
        var externalAgentSelection = ResolveExternalAgentModeSelection(currentMode, packet, workspaceSurface, errors, modeEActivation);
        var externalAgentStrongerModeBlockers = externalAgentSelection.StrongerModeBlockers
            .Where(IsBlockingExternalAgentModeBlocker)
            .ToArray();
        var firstExternalAgentStrongerModeBlocker = externalAgentStrongerModeBlockers.FirstOrDefault();

        return new RuntimeAgentWorkingModesSurface
        {
            WorkingModesDocumentPath = workingModesDocumentPath,
            CollaborationPlaneDocumentPath = collaborationPlaneDocumentPath,
            CliFirstDocumentPath = cliFirstDocumentPath,
            ImplementationPlanPath = implementationPlanPath,
            OverallPosture = errors.Count == 0
                ? "runtime_agent_working_modes_ready"
                : "blocked_by_agent_working_mode_doctrine_gaps",
            CurrentMode = currentMode,
            CurrentModeSummary = RuntimeWorkingModesSemantics.ResolveCurrentModeSummary(currentMode, workspaceSurface),
            StrongestRuntimeSupportedMode = strongestMode,
            ExternalAgentRecommendedMode = externalAgentSelection.RecommendedMode,
            ExternalAgentRecommendationPosture = externalAgentSelection.RecommendationPosture,
            ExternalAgentRecommendationSummary = externalAgentSelection.Summary,
            ExternalAgentRecommendedAction = externalAgentSelection.RecommendedAction,
            ExternalAgentConstraintTier = externalAgentSelection.ConstraintTier,
            ExternalAgentConstraintSummary = externalAgentSelection.ConstraintSummary,
            ExternalAgentStrongerModeBlockerCount = externalAgentStrongerModeBlockers.Length,
            ExternalAgentFirstStrongerModeBlockerId = firstExternalAgentStrongerModeBlocker?.BlockerId,
            ExternalAgentFirstStrongerModeBlockerTargetMode = firstExternalAgentStrongerModeBlocker?.TargetModeId,
            ExternalAgentFirstStrongerModeBlockerRequiredAction = firstExternalAgentStrongerModeBlocker?.RequiredAction,
            ExternalAgentFirstStrongerModeBlockerConstraintClass = firstExternalAgentStrongerModeBlocker?.ConstraintClass,
            ExternalAgentFirstStrongerModeBlockerEnforcementLevel = firstExternalAgentStrongerModeBlocker?.EnforcementLevel,
            PlanningCouplingPosture = planningCouplingPosture,
            PlanningCouplingSummary = RuntimeWorkingModesSemantics.ResolvePlanningCouplingSummary(planningCouplingPosture, status, packet, workspaceSurface),
            PlanHandle = packet?.PlanHandle,
            PlanningCardId = packet?.PlanningCardId ?? status.Draft?.ActivePlanningCard?.PlanningCardId,
            ManagedWorkspacePosture = workspaceSurface.OverallPosture,
            PathPolicyEnforcementState = workspaceSurface.PathPolicyEnforcementState,
            CollaborationPlaneSummary = "CARVES stays CLI-first, ACP-second, MCP-optional, and remains the planning substrate, collaboration plane, and only official truth-ingress authority across agent working modes.",
            ModeEOperationalActivationState = modeEActivation.State,
            ModeEOperationalActivationSummary = modeEActivation.Summary,
            ModeEActivationTaskId = modeEActivation.TaskId,
            ModeEActivationResultReturnChannel = modeEActivation.ResultReturnChannel,
            ModeEActivationCommands = modeEActivation.Commands,
            ModeEActivationRecommendedNextAction = modeEActivation.RecommendedNextAction,
            ModeEActivationBlockingCheckCount = modeEActivationBlockingChecks.Length,
            ModeEActivationFirstBlockingCheckId = firstModeEActivationBlockingCheck?.CheckId,
            ModeEActivationFirstBlockingCheckSummary = firstModeEActivationBlockingCheck?.Summary,
            ModeEActivationFirstBlockingCheckRequiredAction = firstModeEActivationBlockingCheck?.RequiredAction,
            ModeEActivationPlaybookSummary = modeEActivation.PlaybookSummary,
            ModeEActivationPlaybookStepCount = modeEActivation.PlaybookSteps.Count,
            ModeEActivationFirstPlaybookStepCommand = firstModeEActivationPlaybookStep?.Command,
            ModeEActivationFirstPlaybookStepSummary = firstModeEActivationPlaybookStep?.Summary,
            ModeEActivationPlaybookSteps = modeEActivation.PlaybookSteps,
            ModeEActivationChecks = modeEActivation.Checks,
            ExternalAgentStrongerModeBlockers = externalAgentSelection.StrongerModeBlockers,
            ConstraintClasses = BuildConstraintClasses(),
            SupportedModes =
            [
                BuildMode(
                    "mode_a_open_repo_advisory",
                    "Mode A · Open Repo Advisory",
                    "soft",
                    "p0_passive_guidance",
                    "portable",
                    "active_compatibility_baseline",
                    "Official repo is open directly; Runtime doctrine and read surfaces guide work, but hard enforcement is absent."),
                BuildMode(
                    "mode_b_open_repo_with_vendor_native_guard",
                    "Mode B · Open Repo With Vendor-Native Guard",
                    "soft_plus_vendor",
                    "p0_or_p1",
                    "vendor_specific",
                    "deferred_to_phase_8",
                    "Vendor-native permissions may reinforce Runtime policy, but they are acceleration only and not the portable foundation."),
                BuildMode(
                    "mode_c_task_bound_workspace",
                    "Mode C · Task-Bound Workspace",
                    "hard",
                    "p2_task_bound_guidance",
                    "portable",
                    "implemented",
                    "Formal planning and host-routed acceptance issue one task-bound workspace so official repo truth is no longer the default writable surface."),
                BuildMode(
                    "mode_d_scoped_task_workspace",
                    "Mode D · Scoped Task Workspace",
                    "hard_plus_scope_policy",
                    "p2_task_bound_guidance",
                    "portable",
                    ResolveModeDStatus(workspaceSurface),
                    "Task-bound workspace carries a Mode D hardening profile so host-only, deny, and lease-scope escape mutations fail closed before official ingress."),
                BuildMode(
                    "mode_e_brokered_execution",
                    "Mode E · Brokered Execution",
                    "hardest",
                    "p3_host_mediated_planning",
                    "portable",
                    "implemented_task_scoped",
                    "Task-scoped packet/result mediation is available through runtime-brokered-execution <task-id>, execution-packet, packet-enforcement, and host-routed review/writeback."),
            ],
            RecommendedNextAction = ResolveRecommendedNextAction(packet, workspaceSurface, errors, modeEActivation),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not claim that advisory mode disappeared; Mode A remains the compatibility baseline.",
                "This surface does not claim that vendor-native guards are the portable Runtime foundation.",
                "This surface does not claim that Mode E provides OS isolation, remote worker transport, broad ACP packaging, or broad MCP packaging.",
            ],
        };
    }

    private static RuntimeAgentWorkingModeDescriptorSurface BuildMode(
        string modeId,
        string displayName,
        string constraintStrength,
        string planningCoupling,
        string portability,
        string runtimeStatus,
        string summary)
    {
        return new RuntimeAgentWorkingModeDescriptorSurface
        {
            ModeId = modeId,
            DisplayName = displayName,
            ConstraintStrength = constraintStrength,
            PlanningCoupling = planningCoupling,
            Portability = portability,
            RuntimeStatus = runtimeStatus,
            Summary = summary,
        };
    }

    private static string ResolveRecommendedNextAction(
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface,
        IReadOnlyList<string> errors,
        ModeEOperationalActivation modeEActivation)
    {
        if (errors.Count > 0)
        {
            return "Restore the missing working-mode doctrine anchors before treating Phase 7 collaboration posture as governed Runtime behavior.";
        }

        if (packet is null)
        {
            return "Use `plan init [candidate-card-id]` when work should enter formal planning; until then, advisory mode remains the compatibility baseline.";
        }

        if (string.Equals(modeEActivation.State, "task_scoped_activation_available", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(modeEActivation.TaskId))
        {
            return $"Use `inspect runtime-brokered-execution {modeEActivation.TaskId}` before handing execution to a packet-bound worker; return results through `{modeEActivation.ResultReturnChannel}`.";
        }

        if (workspaceSurface.ActiveLeases.Count == 0)
        {
            return workspaceSurface.RecommendedNextAction;
        }

        return "Query `runtime-formal-planning-posture`, `status`, or `workbench overview` to keep current mode, plan handle, and workspace posture aligned while work stays inside the active leased workspace.";
    }

    private static ExternalAgentModeSelection ResolveExternalAgentModeSelection(
        string currentMode,
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface,
        IReadOnlyList<string> errors,
        ModeEOperationalActivation modeEActivation)
    {
        var taskId = packet?.LinkedTruth.TaskIds.FirstOrDefault();
        var hasPacket = packet is not null;
        var hasTask = !string.IsNullOrWhiteSpace(taskId);
        var hasActiveLease = workspaceSurface.ActiveLeases.Count > 0;
        var modeDActive = string.Equals(currentMode, "mode_d_scoped_task_workspace", StringComparison.Ordinal);
        var modeEReady = string.Equals(modeEActivation.State, "task_scoped_activation_available", StringComparison.Ordinal)
            && hasTask;

        if (errors.Count > 0)
        {
            return new ExternalAgentModeSelection(
                "mode_a_open_repo_advisory",
                "blocked_by_working_mode_doctrine_gaps",
                "Runtime cannot recommend a stronger external-agent mode while working-mode doctrine anchors are missing.",
                "Restore the missing working-mode doctrine anchors, then reinspect `runtime-agent-working-modes`.",
                "soft_advisory",
                "Only advisory guidance is available until Runtime can validate its doctrine anchors.",
                [
                    BuildExternalAgentBlocker(
                        "working_mode_doctrine_anchors_available",
                        "mode_c_task_bound_workspace",
                        "hard_runtime_prerequisite",
                        "runtime_read_model_validation",
                        "Working-mode doctrine anchors are missing, so Runtime cannot safely recommend stronger modes.",
                        "restore missing working-mode doctrine anchors",
                        "inspect runtime-agent-working-modes"),
                ]);
        }

        if (modeEReady)
        {
            return new ExternalAgentModeSelection(
                "mode_e_brokered_execution",
                "mode_e_recommended_for_packet_bound_handoff",
                $"External agent handoff should use Mode E for task '{taskId}' because a task-scoped brokered result channel is available.",
                $"Run `inspect runtime-brokered-execution {taskId}` before handing work to a packet-bound worker; return results through `{modeEActivation.ResultReturnChannel}`.",
                "hard_runtime_gate",
                "Mode E is enforceable through Runtime-owned packet/result mediation and review preflight; it is not direct write authority over official truth roots.",
                []);
        }

        if (modeDActive)
        {
            return new ExternalAgentModeSelection(
                "mode_d_scoped_task_workspace",
                "mode_d_recommended_for_scoped_workspace",
                "External agent work should stay in the active scoped task workspace; stronger brokered execution still needs task-scoped Mode E activation.",
                "Continue through the active workspace lease, or reinspect `runtime-agent-working-modes` after Mode E activation prerequisites change.",
                "hard_runtime_gate",
                "Mode D is enforceable through leased workspace scope and path-policy checks before official ingress.",
                [
                    BuildExternalAgentBlocker(
                        "mode_e_task_scoped_activation_available",
                        "mode_e_brokered_execution",
                        "hard_runtime_prerequisite",
                        "host_mediated_packet_result_gate",
                        modeEActivation.Summary,
                        modeEActivation.RecommendedNextAction,
                        ResolveModeERequiredCommand(taskId)),
                ]);
        }

        if (hasTask)
        {
            return new ExternalAgentModeSelection(
                "mode_c_task_bound_workspace",
                "mode_c_recommended_workspace_issue_required",
                $"External agent work should enter Mode C for task '{taskId}', but a managed workspace lease must be issued before file mutation.",
                $"Run `plan issue-workspace {taskId}` before giving the agent writable workspace access.",
                "hard_runtime_gate",
                "Mode C becomes enforceable when Runtime issues a task-bound workspace instead of letting the agent write directly to official repo truth.",
                [
                    BuildExternalAgentBlocker(
                        "managed_workspace_lease_available",
                        "mode_d_scoped_task_workspace",
                        "hard_runtime_prerequisite",
                        "managed_workspace_lease_gate",
                        "Mode D requires an active managed workspace lease with scoped path policy.",
                        $"run `plan issue-workspace {taskId}`",
                        $"plan issue-workspace {taskId}"),
                    BuildExternalAgentBlocker(
                        "mode_e_task_scoped_activation_available",
                        "mode_e_brokered_execution",
                        "hard_runtime_prerequisite",
                        "host_mediated_packet_result_gate",
                        modeEActivation.Summary,
                        modeEActivation.RecommendedNextAction,
                        ResolveModeERequiredCommand(taskId)),
                ]);
        }

        if (hasPacket)
        {
            return new ExternalAgentModeSelection(
                "mode_a_open_repo_advisory",
                "formal_planning_active_without_bound_task",
                "External agent execution should remain advisory until the active planning packet is bound to task truth.",
                "Materialize or approve taskgraph truth for the active plan before giving an external agent a hard-mode workspace or brokered execution packet.",
                "soft_advisory",
                "The current interaction can be guided by Runtime planning surfaces, but no general hard execution constraint applies until task truth exists.",
                [
                    BuildExternalAgentBlocker(
                        "bound_task_truth_available",
                        "mode_c_task_bound_workspace",
                        "hard_runtime_prerequisite",
                        "task_truth_binding_gate",
                        "Task-bound workspace modes need one task id from the active planning packet.",
                        "approve or materialize taskgraph truth for the active plan",
                        "approve-taskgraph-draft <draft-id>"),
                    BuildExternalAgentBlocker(
                        "mode_e_task_scoped_activation_available",
                        "mode_e_brokered_execution",
                        "hard_runtime_prerequisite",
                        "host_mediated_packet_result_gate",
                        modeEActivation.Summary,
                        modeEActivation.RecommendedNextAction,
                        ResolveModeERequiredCommand(taskId)),
                ]);
        }

        return new ExternalAgentModeSelection(
            "mode_a_open_repo_advisory",
            hasActiveLease ? "advisory_fallback_with_workspace_state" : "advisory_until_formal_planning",
            "External agents should treat the current repo as Mode A advisory until formal planning creates the packet and task boundary required by stronger modes.",
            "Run `plan init [candidate-card-id]` when the work should become a governed plan instead of a discussion-only advisory session.",
            "soft_advisory",
            "Mode A is prompt and surface guidance only; Runtime cannot prevent direct file edits when the agent is given the official repo as its writable workspace.",
            [
                BuildExternalAgentBlocker(
                    "formal_planning_packet_available",
                    "mode_c_task_bound_workspace",
                    "hard_runtime_prerequisite",
                    "formal_planning_gate",
                    "A formal planning packet is required before task-bound workspace modes can be selected.",
                    "run `plan init [candidate-card-id]`",
                    "plan init [candidate-card-id]"),
                BuildExternalAgentBlocker(
                    "managed_workspace_lease_available",
                    "mode_d_scoped_task_workspace",
                    "hard_runtime_prerequisite",
                    "managed_workspace_lease_gate",
                    "A managed workspace lease is required before scoped workspace enforcement can apply.",
                    "enter formal planning, bind task truth, then run `plan issue-workspace <task-id>`",
                    "plan issue-workspace <task-id>"),
                BuildExternalAgentBlocker(
                    "mode_e_task_scoped_activation_available",
                    "mode_e_brokered_execution",
                    "hard_runtime_prerequisite",
                    "host_mediated_packet_result_gate",
                    modeEActivation.Summary,
                    modeEActivation.RecommendedNextAction,
                    ResolveModeERequiredCommand(taskId)),
            ]);
    }

    private static RuntimeAgentWorkingModeSelectionBlockerSurface BuildExternalAgentBlocker(
        string blockerId,
        string targetModeId,
        string constraintClass,
        string enforcementLevel,
        string summary,
        string requiredAction,
        string requiredCommand)
    {
        return new RuntimeAgentWorkingModeSelectionBlockerSurface
        {
            BlockerId = blockerId,
            TargetModeId = targetModeId,
            State = "blocking",
            ConstraintClass = constraintClass,
            EnforcementLevel = enforcementLevel,
            Summary = summary,
            RequiredAction = requiredAction,
            RequiredCommand = requiredCommand,
        };
    }

    private static IReadOnlyList<RuntimeAgentWorkingModeConstraintClassSurface> BuildConstraintClasses()
    {
        return
        [
            new RuntimeAgentWorkingModeConstraintClassSurface
            {
                ClassId = "soft_advisory",
                EnforcementLevel = "read_side_guidance_only",
                AppliesToModes = ["mode_a_open_repo_advisory"],
                Summary = "Agent is expected to follow CARVES guidance, but Runtime cannot prevent direct edits when official repo files are writable by the agent.",
            },
            new RuntimeAgentWorkingModeConstraintClassSurface
            {
                ClassId = "vendor_native_optional",
                EnforcementLevel = "vendor_specific_reinforcement",
                AppliesToModes = ["mode_b_open_repo_with_vendor_native_guard"],
                Summary = "Codex or Claude native guards may reinforce policy, but they are optional accelerators and not the portable default path.",
            },
            new RuntimeAgentWorkingModeConstraintClassSurface
            {
                ClassId = "hard_runtime_gate",
                EnforcementLevel = "portable_runtime_enforced",
                AppliesToModes = ["mode_c_task_bound_workspace", "mode_d_scoped_task_workspace", "mode_e_brokered_execution"],
                Summary = "Runtime gates work through formal planning, task-bound workspace leases, scoped path policy, packet/result mediation, or review preflight before official truth ingress.",
            },
        ];
    }

    private static bool IsBlockingExternalAgentModeBlocker(RuntimeAgentWorkingModeSelectionBlockerSurface blocker)
    {
        return string.Equals(blocker.State, "blocking", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveModeERequiredCommand(string? taskId)
    {
        return string.IsNullOrWhiteSpace(taskId)
            ? "inspect runtime-agent-working-modes"
            : $"inspect runtime-brokered-execution {taskId}";
    }

    private static string ResolveModeDStatus(RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        return workspaceSurface.ModeDHardeningState switch
        {
            "active" => "hardening_active",
            "plan_init_required" or "waiting_for_bound_task_truth" or "workspace_issue_required" => "hardening_ready_prerequisites_missing",
            "incomplete_lease_scope" or "blocked_by_path_policy_gaps" or "blocked_by_mode_d_doctrine_gaps" => "hardening_blocked",
            _ => "planned",
        };
    }

    private static ModeEOperationalActivation ResolveModeEOperationalActivation(
        FormalPlanningPacket? packet,
        IReadOnlyList<string> errors)
    {
        var taskId = packet?.LinkedTruth.TaskIds.FirstOrDefault();
        var resultReturnChannel = string.IsNullOrWhiteSpace(taskId)
            ? null
            : $".ai/execution/{taskId}/result.json";

        if (errors.Count > 0)
        {
            return BuildModeEActivation(
                "blocked_by_working_mode_doctrine_gaps",
                "Mode E activation is blocked because working-mode doctrine anchors are missing.",
                "Restore the missing working-mode doctrine anchors before selecting Mode E brokered execution.",
                packet is not null,
                taskId,
                resultReturnChannel);
        }

        if (packet is null)
        {
            return BuildModeEActivation(
                "plan_init_required_before_mode_e_activation",
                "Mode E is implemented as a task-scoped brokered lane, but activation needs a formal planning packet first.",
                "Run `plan init [candidate-card-id]` before selecting Mode E brokered execution.",
                false,
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            return BuildModeEActivation(
                "waiting_for_bound_task_truth",
                "Mode E activation has a formal planning packet, but no bound task truth is available yet.",
                "Approve or materialize taskgraph truth for the active plan before selecting Mode E brokered execution.",
                true,
                null,
                null);
        }

        return BuildModeEActivation(
            "task_scoped_activation_available",
            $"Mode E can be activated for task '{taskId}' through the brokered execution inspect chain and result-return channel.",
            $"Run `inspect runtime-brokered-execution {taskId}` before handing execution to a packet-bound worker; return results through `{resultReturnChannel}`.",
            true,
            taskId,
            resultReturnChannel);
    }

    private static ModeEOperationalActivation BuildModeEActivation(
        string state,
        string summary,
        string recommendedNextAction,
        bool hasPacket,
        string? taskId,
        string? resultReturnChannel)
    {
        var hasTask = !string.IsNullOrWhiteSpace(taskId);
        var commands = hasTask
            ? new[]
            {
                $"inspect runtime-brokered-execution {taskId}",
                $"api runtime-brokered-execution {taskId}",
                $"inspect execution-packet {taskId}",
                $"inspect packet-enforcement {taskId}",
            }
            : Array.Empty<string>();
        var playbookSteps = BuildModeEActivationPlaybook(state, taskId, resultReturnChannel);

        return new ModeEOperationalActivation(
            state,
            summary,
            taskId,
            resultReturnChannel,
            commands,
            recommendedNextAction,
            BuildModeEActivationPlaybookSummary(state),
            playbookSteps,
            [
                BuildActivationCheck(
                    "mode_e_profile_projected",
                    "satisfied",
                    "Mode E brokered execution is projected as an implemented task-scoped Runtime profile.",
                    "none"),
                BuildActivationCheck(
                    "formal_planning_packet_available",
                    hasPacket ? "satisfied" : "missing",
                    hasPacket
                        ? "A formal planning packet is available for Mode E activation."
                        : "Mode E activation requires `plan init` before task-scoped brokered execution can be selected.",
                    hasPacket ? "none" : "run `plan init [candidate-card-id]`"),
                BuildActivationCheck(
                    "bound_task_truth_available",
                    hasTask ? "satisfied" : "missing",
                    hasTask
                        ? $"Task '{taskId}' is bound to the current planning packet."
                        : "No bound task id is available for brokered execution.",
                    hasTask ? "none" : "approve or materialize taskgraph truth for the active plan"),
                BuildActivationCheck(
                    "brokered_execution_surface_available",
                    hasTask ? "satisfied" : "waiting_for_task",
                    hasTask
                        ? $"Use `inspect runtime-brokered-execution {taskId}` to inspect packet, enforcement, and truth-ingress state."
                        : "The brokered execution surface is task-scoped and needs a task id.",
                    hasTask ? "none" : "provide a bound task id"),
                BuildActivationCheck(
                    "result_return_channel_projected",
                    resultReturnChannel is not null ? "satisfied" : "waiting_for_task",
                    resultReturnChannel is not null
                        ? $"Worker result return is projected at `{resultReturnChannel}`."
                        : "No result-return channel is projected until a task id is bound.",
                    resultReturnChannel is not null ? "none" : "bind a task before issuing Mode E work"),
            ]);
    }

    private static RuntimeAgentWorkingModeActivationCheckSurface BuildActivationCheck(
        string checkId,
        string state,
        string summary,
        string requiredAction)
    {
        return new RuntimeAgentWorkingModeActivationCheckSurface
        {
            CheckId = checkId,
            State = state,
            Summary = summary,
            RequiredAction = requiredAction,
        };
    }

    private static IReadOnlyList<RuntimeAgentWorkingModeActivationPlaybookStepSurface> BuildModeEActivationPlaybook(
        string state,
        string? taskId,
        string? resultReturnChannel)
    {
        if (string.Equals(state, "task_scoped_activation_available", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(taskId))
        {
            return
            [
                BuildActivationPlaybookStep(
                    1,
                    "inspect_brokered_execution",
                    "Inspect brokered execution readiness",
                    $"inspect runtime-brokered-execution {taskId}",
                    "Read packet, enforcement, result-return, and truth-ingress state before handing work to a packet-bound worker.",
                    state),
                BuildActivationPlaybookStep(
                    2,
                    "inspect_execution_packet",
                    "Inspect execution packet",
                    $"inspect execution-packet {taskId}",
                    "Confirm the packet scope and worker-allowed actions before issuing Mode E work.",
                    state),
                BuildActivationPlaybookStep(
                    3,
                    "inspect_packet_enforcement",
                    "Inspect packet enforcement",
                    $"inspect packet-enforcement {taskId}",
                    "Confirm enforcement will evaluate returned material before review/writeback.",
                    state),
                BuildActivationPlaybookStep(
                    4,
                    "return_brokered_result",
                    "Return brokered result",
                    $"write result to {resultReturnChannel ?? $".ai/execution/{taskId}/result.json"}",
                    "Return worker results through the brokered result channel; planner review and host writeback remain the official truth ingress.",
                    state),
            ];
        }

        if (string.Equals(state, "waiting_for_bound_task_truth", StringComparison.Ordinal))
        {
            return
            [
                BuildActivationPlaybookStep(
                    1,
                    "materialize_task_truth",
                    "Materialize bound task truth",
                    "approve-taskgraph-draft <draft-id>",
                    "Approve or materialize taskgraph truth for the active plan so Mode E has a concrete task id.",
                    state),
                BuildActivationPlaybookStep(
                    2,
                    "reinspect_working_modes",
                    "Reinspect Mode E activation",
                    "inspect runtime-agent-working-modes",
                    "Re-read activation posture after task truth is bound.",
                    state),
            ];
        }

        if (string.Equals(state, "blocked_by_working_mode_doctrine_gaps", StringComparison.Ordinal))
        {
            return
            [
                BuildActivationPlaybookStep(
                    1,
                    "restore_working_mode_doctrine",
                    "Restore working-mode doctrine",
                    "inspect runtime-agent-working-modes",
                    "Repair missing doctrine anchors before selecting Mode E brokered execution.",
                    state),
            ];
        }

        return
        [
            BuildActivationPlaybookStep(
                1,
                "enter_formal_planning",
                "Enter formal planning",
                "plan init [candidate-card-id]",
                "Create or select an active planning card before selecting Mode E brokered execution.",
                state),
            BuildActivationPlaybookStep(
                2,
                "reinspect_working_modes",
                "Reinspect Mode E activation",
                "inspect runtime-agent-working-modes",
                "Re-read activation posture after formal planning exists.",
                state),
        ];
    }

    private static string BuildModeEActivationPlaybookSummary(string state)
    {
        return state switch
        {
            "task_scoped_activation_available" => "Mode E activation playbook is task-scoped: inspect brokered execution, inspect packet, inspect enforcement, then return results through the brokered result channel.",
            "waiting_for_bound_task_truth" => "Mode E activation playbook is waiting for bound task truth before a task-scoped brokered handoff can be inspected.",
            "blocked_by_working_mode_doctrine_gaps" => "Mode E activation playbook is blocked until working-mode doctrine anchors are restored.",
            _ => "Mode E activation playbook starts with formal planning: run plan init, then reinspect working modes.",
        };
    }

    private static RuntimeAgentWorkingModeActivationPlaybookStepSurface BuildActivationPlaybookStep(
        int order,
        string stepId,
        string title,
        string command,
        string summary,
        string appliesWhen)
    {
        return new RuntimeAgentWorkingModeActivationPlaybookStepSurface
        {
            Order = order,
            StepId = stepId,
            Title = title,
            Command = command,
            Summary = summary,
            AppliesWhen = appliesWhen,
        };
    }

    private static bool IsBlockingActivationCheck(RuntimeAgentWorkingModeActivationCheckSurface check)
    {
        return !string.Equals(check.State, "satisfied", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(check.RequiredAction, "none", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private sealed record ModeEOperationalActivation(
        string State,
        string Summary,
        string? TaskId,
        string? ResultReturnChannel,
        IReadOnlyList<string> Commands,
        string RecommendedNextAction,
        string PlaybookSummary,
        IReadOnlyList<RuntimeAgentWorkingModeActivationPlaybookStepSurface> PlaybookSteps,
        IReadOnlyList<RuntimeAgentWorkingModeActivationCheckSurface> Checks);

    private sealed record ExternalAgentModeSelection(
        string RecommendedMode,
        string RecommendationPosture,
        string Summary,
        string RecommendedAction,
        string ConstraintTier,
        string ConstraintSummary,
        IReadOnlyList<RuntimeAgentWorkingModeSelectionBlockerSurface> StrongerModeBlockers);
}
