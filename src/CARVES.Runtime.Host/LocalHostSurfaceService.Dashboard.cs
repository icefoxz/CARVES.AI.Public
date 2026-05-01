using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildDashboardReadModel()
    {
        var graph = services.TaskGraphService.Load();
        var session = services.DevLoopService.GetSession();
        var dispatchProjection = services.DispatchProjectionService.Build(graph, session, services.SystemConfig.MaxParallelTasks);
        var completedTaskIds = graph.CompletedTaskIds();
        var platformStatus = services.OperatorApiService.GetPlatformStatus();
        var interaction = services.InteractionLayerService.GetSnapshot(session);
        var hostSession = new HostSessionService(services.Paths).Load();
        var runtimeManifest = new RuntimeManifestService(services.Paths).Load();
        var projectionHealth = new MarkdownProjectionHealthService(services.Paths).Load();
        var sessionGatewayGovernanceAssist = services.PlatformDashboardService.BuildSessionGatewayGovernanceAssistSurface();
        var acceptanceContractIngressPolicy = services.PlatformDashboardService.BuildAcceptanceContractIngressPolicySurface();
        var agentWorkingModes = services.PlatformDashboardService.BuildAgentWorkingModesSurface();
        var formalPlanningPosture = services.PlatformDashboardService.BuildFormalPlanningPostureSurface();
        var vendorNativeAcceleration = services.PlatformDashboardService.BuildVendorNativeAccelerationSurface();

        return new JsonObject
        {
            ["kind"] = "dashboard_summary",
            ["host_session"] = new JsonObject
            {
                ["session_id"] = hostSession?.SessionId,
                ["status"] = hostSession?.Status.ToString().ToLowerInvariant() ?? "none",
                ["control_state"] = hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running",
                ["last_control_action"] = hostSession?.LastControlAction.ToString().ToLowerInvariant() ?? "started",
                ["last_control_reason"] = hostSession?.LastControlReason ?? "Resident host session started.",
                ["attached_repo_count"] = hostSession?.AttachedRepos.Count ?? 0,
            },
            ["host_control"] = new JsonObject
            {
                ["state"] = hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running",
                ["reason"] = hostSession?.LastControlReason ?? "Resident host session started.",
                ["at"] = hostSession?.LastControlAt,
            },
            ["interaction"] = new JsonObject
            {
                ["protocol_mode"] = interaction.ProtocolMode,
                ["conversation_phase"] = interaction.Protocol.CurrentPhase.ToString().ToLowerInvariant(),
                ["intent_state"] = interaction.Intent.State.ToString().ToLowerInvariant(),
                ["prompt_kernel"] = $"{interaction.PromptKernel.KernelId}@{interaction.PromptKernel.Version}",
                ["project_understanding_state"] = interaction.ProjectUnderstanding.State.ToString().ToLowerInvariant(),
                ["project_understanding_action"] = interaction.ProjectUnderstanding.Action,
                ["project_summary"] = interaction.ProjectUnderstanding.Summary,
                ["next_action"] = interaction.RecommendedNextAction,
            },
            ["dispatch"] = new JsonObject
            {
                ["state"] = dispatchProjection.State,
                ["summary"] = dispatchProjection.Summary,
                ["idle_reason_code"] = dispatchProjection.IdleReason,
                ["idle_reason"] = services.DispatchProjectionService.DescribeIdleReason(dispatchProjection.IdleReason),
                ["next_dispatchable_task"] = dispatchProjection.NextTaskId,
                ["next_ready_task"] = dispatchProjection.NextTaskId,
                ["dispatchable_task_count"] = dispatchProjection.ReadyTaskCount,
                ["ready_task_count"] = dispatchProjection.ReadyTaskCount,
                ["acceptance_contract_gap_count"] = dispatchProjection.AcceptanceContractGapCount,
                ["plan_required_block_count"] = dispatchProjection.PlanRequiredBlockCount,
                ["workspace_required_block_count"] = dispatchProjection.WorkspaceRequiredBlockCount,
                ["first_blocked_task_id"] = dispatchProjection.FirstBlockedTaskId,
                ["first_blocking_check_id"] = dispatchProjection.FirstBlockingCheckId,
                ["first_blocking_check_summary"] = dispatchProjection.FirstBlockingCheckSummary,
                ["first_blocking_check_required_action"] = dispatchProjection.FirstBlockingCheckRequiredAction,
                ["first_blocking_check_required_command"] = dispatchProjection.FirstBlockingCheckRequiredCommand,
                ["recommended_next_action"] = dispatchProjection.RecommendedNextAction,
                ["recommended_next_command"] = dispatchProjection.RecommendedNextCommand,
                ["running_task_count"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Running),
                ["review_task_count"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
                ["blocked_task_count"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Blocked),
            },
            ["accepted_operations"] = BuildAcceptedOperationsNode(),
            ["platform"] = new JsonObject
            {
                ["registered_repo_count"] = platformStatus.RegisteredRepoCount,
                ["running_instance_count"] = platformStatus.RunningInstanceCount,
                ["worker_node_count"] = platformStatus.WorkerNodeCount,
                ["active_lease_count"] = platformStatus.ActiveLeaseCount,
            },
            ["runtime_truth"] = new JsonObject
            {
                ["repo_id"] = runtimeManifest?.RepoId,
                ["runtime_status"] = runtimeManifest?.RuntimeStatus,
                ["state"] = runtimeManifest?.State.ToString().ToLowerInvariant() ?? "missing",
            },
            ["projection_writeback"] = new JsonObject
            {
                ["state"] = projectionHealth.State,
                ["summary"] = projectionHealth.Summary,
                ["consecutive_failure_count"] = projectionHealth.ConsecutiveFailureCount,
                ["updated_at"] = projectionHealth.UpdatedAt,
            },
            ["acceptance_contract_ingress_policy"] = new JsonObject
            {
                ["overall_posture"] = acceptanceContractIngressPolicy.OverallPosture,
                ["planning_truth_mutation_policy"] = acceptanceContractIngressPolicy.PlanningTruthMutationPolicy,
                ["execution_dispatch_policy"] = acceptanceContractIngressPolicy.ExecutionDispatchPolicy,
                ["policy_summary"] = acceptanceContractIngressPolicy.PolicySummary,
                ["document_path"] = acceptanceContractIngressPolicy.PolicyDocumentPath,
            },
            ["agent_working_modes"] = new JsonObject
            {
                ["overall_posture"] = agentWorkingModes.OverallPosture,
                ["current_mode"] = agentWorkingModes.CurrentMode,
                ["current_mode_summary"] = agentWorkingModes.CurrentModeSummary,
                ["strongest_runtime_supported_mode"] = agentWorkingModes.StrongestRuntimeSupportedMode,
                ["external_agent_recommended_mode"] = agentWorkingModes.ExternalAgentRecommendedMode,
                ["external_agent_recommendation_posture"] = agentWorkingModes.ExternalAgentRecommendationPosture,
                ["external_agent_recommendation_summary"] = agentWorkingModes.ExternalAgentRecommendationSummary,
                ["external_agent_recommended_action"] = agentWorkingModes.ExternalAgentRecommendedAction,
                ["external_agent_constraint_tier"] = agentWorkingModes.ExternalAgentConstraintTier,
                ["external_agent_constraint_summary"] = agentWorkingModes.ExternalAgentConstraintSummary,
                ["external_agent_stronger_mode_blocker_count"] = agentWorkingModes.ExternalAgentStrongerModeBlockerCount,
                ["external_agent_first_stronger_mode_blocker_id"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId,
                ["external_agent_first_stronger_mode_blocker_target_mode"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerTargetMode,
                ["external_agent_first_stronger_mode_blocker_required_action"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction,
                ["external_agent_first_stronger_mode_blocker_constraint_class"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerConstraintClass,
                ["external_agent_first_stronger_mode_blocker_enforcement_level"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerEnforcementLevel,
                ["planning_coupling_posture"] = agentWorkingModes.PlanningCouplingPosture,
                ["planning_coupling_summary"] = agentWorkingModes.PlanningCouplingSummary,
                ["plan_handle"] = agentWorkingModes.PlanHandle,
                ["planning_card_id"] = agentWorkingModes.PlanningCardId,
                ["managed_workspace_posture"] = agentWorkingModes.ManagedWorkspacePosture,
                ["path_policy_enforcement_state"] = agentWorkingModes.PathPolicyEnforcementState,
                ["mode_e_operational_activation_state"] = agentWorkingModes.ModeEOperationalActivationState,
                ["mode_e_operational_activation_summary"] = agentWorkingModes.ModeEOperationalActivationSummary,
                ["mode_e_activation_task_id"] = agentWorkingModes.ModeEActivationTaskId,
                ["mode_e_activation_result_return_channel"] = agentWorkingModes.ModeEActivationResultReturnChannel,
                ["mode_e_activation_commands"] = ToJsonArray(agentWorkingModes.ModeEActivationCommands),
                ["mode_e_activation_recommended_next_action"] = agentWorkingModes.ModeEActivationRecommendedNextAction,
                ["mode_e_activation_blocking_check_count"] = agentWorkingModes.ModeEActivationBlockingCheckCount,
                ["mode_e_activation_first_blocking_check_id"] = agentWorkingModes.ModeEActivationFirstBlockingCheckId,
                ["mode_e_activation_first_blocking_check_summary"] = agentWorkingModes.ModeEActivationFirstBlockingCheckSummary,
                ["mode_e_activation_first_blocking_check_required_action"] = agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction,
                ["mode_e_activation_playbook_summary"] = agentWorkingModes.ModeEActivationPlaybookSummary,
                ["mode_e_activation_playbook_step_count"] = agentWorkingModes.ModeEActivationPlaybookStepCount,
                ["mode_e_activation_first_playbook_step_command"] = agentWorkingModes.ModeEActivationFirstPlaybookStepCommand,
                ["mode_e_activation_first_playbook_step_summary"] = agentWorkingModes.ModeEActivationFirstPlaybookStepSummary,
            },
            ["formal_planning_posture"] = new JsonObject
            {
                ["overall_posture"] = formalPlanningPosture.OverallPosture,
                ["intent_state"] = formalPlanningPosture.IntentState,
                ["guided_planning_posture"] = formalPlanningPosture.GuidedPlanningPosture,
                ["formal_planning_state"] = formalPlanningPosture.FormalPlanningState,
                ["formal_planning_entry_trigger_state"] = formalPlanningPosture.FormalPlanningEntryTriggerState,
                ["formal_planning_entry_command"] = formalPlanningPosture.FormalPlanningEntryCommand,
                ["formal_planning_entry_recommended_next_action"] = formalPlanningPosture.FormalPlanningEntryRecommendedNextAction,
                ["formal_planning_entry_summary"] = formalPlanningPosture.FormalPlanningEntrySummary,
                ["active_planning_slot_state"] = formalPlanningPosture.ActivePlanningSlotState,
                ["active_planning_slot_can_initialize"] = formalPlanningPosture.ActivePlanningSlotCanInitialize,
                ["active_planning_slot_conflict_reason"] = formalPlanningPosture.ActivePlanningSlotConflictReason,
                ["active_planning_slot_remediation_action"] = formalPlanningPosture.ActivePlanningSlotRemediationAction,
                ["planning_card_invariant_state"] = formalPlanningPosture.PlanningCardInvariantState,
                ["planning_card_invariant_can_export_governed_truth"] = formalPlanningPosture.PlanningCardInvariantCanExportGovernedTruth,
                ["planning_card_invariant_summary"] = formalPlanningPosture.PlanningCardInvariantSummary,
                ["planning_card_invariant_remediation_action"] = formalPlanningPosture.PlanningCardInvariantRemediationAction,
                ["planning_card_invariant_block_count"] = formalPlanningPosture.PlanningCardInvariantBlockCount,
                ["planning_card_invariant_violation_count"] = formalPlanningPosture.PlanningCardInvariantViolationCount,
                ["active_planning_card_fill_state"] = formalPlanningPosture.ActivePlanningCardFillState,
                ["active_planning_card_fill_completion_posture"] = formalPlanningPosture.ActivePlanningCardFillCompletionPosture,
                ["active_planning_card_fill_ready_for_recommended_export"] = formalPlanningPosture.ActivePlanningCardFillReadyForRecommendedExport,
                ["active_planning_card_fill_summary"] = formalPlanningPosture.ActivePlanningCardFillSummary,
                ["active_planning_card_fill_recommended_next_action"] = formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction,
                ["active_planning_card_fill_next_missing_field_path"] = formalPlanningPosture.ActivePlanningCardFillNextMissingFieldPath,
                ["active_planning_card_fill_required_field_count"] = formalPlanningPosture.ActivePlanningCardFillRequiredFieldCount,
                ["active_planning_card_fill_missing_required_field_count"] = formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount,
                ["active_planning_card_fill_missing_field_paths"] = ToJsonArray(formalPlanningPosture.ActivePlanningCardFillMissingFieldPaths),
                ["plan_handle"] = formalPlanningPosture.PlanHandle,
                ["planning_card_id"] = formalPlanningPosture.PlanningCardId,
                ["packet_available"] = formalPlanningPosture.PacketAvailable,
                ["recommended_next_action"] = formalPlanningPosture.RecommendedNextAction,
                ["managed_workspace_posture"] = formalPlanningPosture.ManagedWorkspacePosture,
                ["plan_required_block_count"] = formalPlanningPosture.PlanRequiredBlockCount,
                ["workspace_required_block_count"] = formalPlanningPosture.WorkspaceRequiredBlockCount,
                ["mode_execution_entry_first_blocked_task_id"] = formalPlanningPosture.ModeExecutionEntryFirstBlockedTaskId,
                ["mode_execution_entry_first_blocking_check_id"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckId,
                ["mode_execution_entry_first_blocking_check_summary"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckSummary,
                ["mode_execution_entry_first_blocking_check_required_action"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckRequiredAction,
                ["mode_execution_entry_first_blocking_check_required_command"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckRequiredCommand,
                ["mode_execution_entry_recommended_next_action"] = formalPlanningPosture.ModeExecutionEntryRecommendedNextAction,
                ["mode_execution_entry_recommended_next_command"] = formalPlanningPosture.ModeExecutionEntryRecommendedNextCommand,
                ["missing_prerequisites"] = ToJsonArray(formalPlanningPosture.MissingPrerequisites),
            },
            ["vendor_native_acceleration"] = new JsonObject
            {
                ["overall_posture"] = vendorNativeAcceleration.OverallPosture,
                ["current_mode"] = vendorNativeAcceleration.CurrentMode,
                ["planning_coupling_posture"] = vendorNativeAcceleration.PlanningCouplingPosture,
                ["formal_planning_posture"] = vendorNativeAcceleration.FormalPlanningPosture,
                ["plan_handle"] = vendorNativeAcceleration.PlanHandle,
                ["planning_card_id"] = vendorNativeAcceleration.PlanningCardId,
                ["managed_workspace_posture"] = vendorNativeAcceleration.ManagedWorkspacePosture,
                ["codex_reinforcement_state"] = vendorNativeAcceleration.CodexReinforcementState,
                ["claude_reinforcement_state"] = vendorNativeAcceleration.ClaudeReinforcementState,
                ["recommended_next_action"] = vendorNativeAcceleration.RecommendedNextAction,
            },
            ["session_gateway_governance_assist"] = BuildSessionGatewayGovernanceAssistDashboardNode(sessionGatewayGovernanceAssist),
        };
    }

    public IReadOnlyList<string> RenderDashboardText(string? repoId = null)
    {
        var summary = BuildDashboardReadModel();
        var interaction = summary["interaction"]?.AsObject() ?? new JsonObject();
        var hostControl = summary["host_control"]?.AsObject() ?? new JsonObject();
        var hostSession = summary["host_session"]?.AsObject() ?? new JsonObject();
        var dispatch = summary["dispatch"]?.AsObject() ?? new JsonObject();
        var acceptedOperations = summary["accepted_operations"]?.AsArray() ?? new JsonArray();
        var platform = summary["platform"]?.AsObject() ?? new JsonObject();
        var runtimeTruth = summary["runtime_truth"]?.AsObject() ?? new JsonObject();
        var projectionWriteback = summary["projection_writeback"]?.AsObject() ?? new JsonObject();
        var acceptanceContractIngressPolicy = summary["acceptance_contract_ingress_policy"]?.AsObject() ?? new JsonObject();
        var agentWorkingModes = summary["agent_working_modes"]?.AsObject() ?? new JsonObject();
        var formalPlanningPosture = summary["formal_planning_posture"]?.AsObject() ?? new JsonObject();
        var vendorNativeAcceleration = summary["vendor_native_acceleration"]?.AsObject() ?? new JsonObject();
        var sessionGatewayGovernanceAssist = summary["session_gateway_governance_assist"]?.AsObject() ?? new JsonObject();
        var sessionGatewayTopPressure = sessionGatewayGovernanceAssist["highest_priority_pressure"]?.AsObject() ?? new JsonObject();

        var lines = new List<string>
        {
            "Platform Overview",
            $"Repo: {repoId ?? runtimeTruth["repo_id"]?.GetValue<string>() ?? "local-repo"}",
            $"Host control: {hostControl["state"]?.GetValue<string>() ?? "running"}",
            $"Host reason: {hostControl["reason"]?.GetValue<string>() ?? "Resident host session started."}",
            $"Host session: {hostSession["session_id"]?.GetValue<string>() ?? "(none)"} [{hostSession["status"]?.GetValue<string>() ?? "none"}]",
            $"Attached repos: {hostSession["attached_repo_count"]?.GetValue<int>() ?? 0}",
            $"Interaction mode: {interaction["protocol_mode"]?.GetValue<string>() ?? "unknown"}",
            $"Conversation phase: {interaction["conversation_phase"]?.GetValue<string>() ?? "unknown"}",
            $"Intent state: {interaction["intent_state"]?.GetValue<string>() ?? "unknown"}",
            $"Prompt kernel: {interaction["prompt_kernel"]?.GetValue<string>() ?? "(none)"}",
            $"Project understanding: {interaction["project_understanding_state"]?.GetValue<string>() ?? "unknown"} ({interaction["project_understanding_action"]?.GetValue<string>() ?? "(none)"})",
            $"Project summary: {interaction["project_summary"]?.GetValue<string>() ?? "(none)"}",
            $"Interaction next action: {interaction["next_action"]?.GetValue<string>() ?? "(none)"}",
            $"Agent working mode: {agentWorkingModes["current_mode"]?.GetValue<string>() ?? "mode_a_open_repo_advisory"}",
            $"External agent recommended mode: {agentWorkingModes["external_agent_recommended_mode"]?.GetValue<string>() ?? "mode_a_open_repo_advisory"}",
            $"External agent recommendation posture: {agentWorkingModes["external_agent_recommendation_posture"]?.GetValue<string>() ?? "advisory_until_formal_planning"}",
            $"External agent constraint tier: {agentWorkingModes["external_agent_constraint_tier"]?.GetValue<string>() ?? "soft_advisory"}",
            $"External agent stronger-mode blockers: {agentWorkingModes["external_agent_stronger_mode_blocker_count"]?.GetValue<int>() ?? 0}",
            $"External agent first stronger-mode blocker: {agentWorkingModes["external_agent_first_stronger_mode_blocker_id"]?.GetValue<string>() ?? "(none)"}",
            $"External agent first stronger-mode blocker action: {agentWorkingModes["external_agent_first_stronger_mode_blocker_required_action"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation: {agentWorkingModes["mode_e_operational_activation_state"]?.GetValue<string>() ?? "plan_init_required_before_mode_e_activation"}",
            $"Mode E activation task: {agentWorkingModes["mode_e_activation_task_id"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation command: {agentWorkingModes["mode_e_activation_commands"]?.AsArray().FirstOrDefault()?.GetValue<string>() ?? "(none)"}",
            $"Mode E result return channel: {agentWorkingModes["mode_e_activation_result_return_channel"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation next action: {agentWorkingModes["mode_e_activation_recommended_next_action"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation blocking checks: {agentWorkingModes["mode_e_activation_blocking_check_count"]?.GetValue<int>() ?? 0}",
            $"Mode E activation first blocker: {agentWorkingModes["mode_e_activation_first_blocking_check_id"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation first blocker action: {agentWorkingModes["mode_e_activation_first_blocking_check_required_action"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation playbook: {agentWorkingModes["mode_e_activation_playbook_summary"]?.GetValue<string>() ?? "(none)"}",
            $"Mode E activation playbook steps: {agentWorkingModes["mode_e_activation_playbook_step_count"]?.GetValue<int>() ?? 0}",
            $"Mode E activation first playbook command: {agentWorkingModes["mode_e_activation_first_playbook_step_command"]?.GetValue<string>() ?? "(none)"}",
            $"Planning coupling: {agentWorkingModes["planning_coupling_posture"]?.GetValue<string>() ?? "p0_passive_guidance"}",
            $"Formal planning posture: {formalPlanningPosture["overall_posture"]?.GetValue<string>() ?? "discussion_only"}",
            $"Formal planning entry trigger: {formalPlanningPosture["formal_planning_entry_trigger_state"]?.GetValue<string>() ?? "discussion_only"}",
            $"Formal planning entry command: {formalPlanningPosture["formal_planning_entry_command"]?.GetValue<string>() ?? "plan init [candidate-card-id]"}",
            $"Formal planning entry next action: {formalPlanningPosture["formal_planning_entry_recommended_next_action"]?.GetValue<string>() ?? "(none)"}",
            $"Active planning slot state: {formalPlanningPosture["active_planning_slot_state"]?.GetValue<string>() ?? "no_intent_draft"}",
            $"Active planning slot conflict reason: {formalPlanningPosture["active_planning_slot_conflict_reason"]?.GetValue<string>() ?? "(none)"}",
            $"Active planning slot remediation: {formalPlanningPosture["active_planning_slot_remediation_action"]?.GetValue<string>() ?? "(none)"}",
            $"Planning card invariant state: {formalPlanningPosture["planning_card_invariant_state"]?.GetValue<string>() ?? "no_active_planning_card"}",
            $"Planning card invariant violations: {formalPlanningPosture["planning_card_invariant_violation_count"]?.GetValue<int>() ?? 0}",
            $"Planning card invariant remediation: {formalPlanningPosture["planning_card_invariant_remediation_action"]?.GetValue<string>() ?? "(none)"}",
            $"Active planning card fill state: {formalPlanningPosture["active_planning_card_fill_state"]?.GetValue<string>() ?? "no_active_planning_card"}",
            $"Active planning card fill missing required fields: {formalPlanningPosture["active_planning_card_fill_missing_required_field_count"]?.GetValue<int>() ?? 0}",
            $"Active planning card fill next action: {formalPlanningPosture["active_planning_card_fill_recommended_next_action"]?.GetValue<string>() ?? "(none)"}",
            $"Active plan handle: {formalPlanningPosture["plan_handle"]?.GetValue<string>() ?? "(none)"}",
            $"Active planning card: {formalPlanningPosture["planning_card_id"]?.GetValue<string>() ?? "(none)"}",
            $"Managed workspace posture: {formalPlanningPosture["managed_workspace_posture"]?.GetValue<string>() ?? "(none)"}",
            $"Vendor-native acceleration: {vendorNativeAcceleration["overall_posture"]?.GetValue<string>() ?? "blocked_by_vendor_native_acceleration_gaps"}",
            $"Codex reinforcement: {vendorNativeAcceleration["codex_reinforcement_state"]?.GetValue<string>() ?? "repo_guard_assets_incomplete"}",
            $"Claude reinforcement: {vendorNativeAcceleration["claude_reinforcement_state"]?.GetValue<string>() ?? "bounded_runtime_qualification_incomplete"}",
            $"Dispatch state: {dispatch["state"]?.GetValue<string>() ?? "dispatch_blocked"} ({dispatch["idle_reason"]?.GetValue<string>() ?? "unknown"})",
            $"Next dispatchable task: {dispatch["next_dispatchable_task"]?.GetValue<string>() ?? "(none)"}",
            $"Acceptance contract gaps: {dispatch["acceptance_contract_gap_count"]?.GetValue<int>() ?? 0}",
            $"Plan-required execution gaps: {dispatch["plan_required_block_count"]?.GetValue<int>() ?? 0}",
            $"Managed workspace gaps: {dispatch["workspace_required_block_count"]?.GetValue<int>() ?? 0}",
            $"Mode C/D entry first blocker: {dispatch["first_blocking_check_id"]?.GetValue<string>() ?? "(none)"}",
            $"Mode C/D entry first blocker command: {dispatch["first_blocking_check_required_command"]?.GetValue<string>() ?? "(none)"}",
            $"Mode C/D entry next command: {dispatch["recommended_next_command"]?.GetValue<string>() ?? "(none)"}",
            $"Running tasks: {dispatch["running_task_count"]?.GetValue<int>() ?? 0}",
            $"Review tasks: {dispatch["review_task_count"]?.GetValue<int>() ?? 0}",
            $"Blocked tasks: {dispatch["blocked_task_count"]?.GetValue<int>() ?? 0}",
            $"Platform repos: {platform["registered_repo_count"]?.GetValue<int>() ?? 0}",
            $"Running instances: {platform["running_instance_count"]?.GetValue<int>() ?? 0}",
            $"Worker nodes: {platform["worker_node_count"]?.GetValue<int>() ?? 0}",
            $"Active leases: {platform["active_lease_count"]?.GetValue<int>() ?? 0}",
            $"Runtime truth: {runtimeTruth["state"]?.GetValue<string>() ?? "missing"} / {runtimeTruth["runtime_status"]?.GetValue<string>() ?? "(none)"}",
            $"Projection writeback: {projectionWriteback["state"]?.GetValue<string>() ?? "healthy"} ({projectionWriteback["summary"]?.GetValue<string>() ?? "Markdown projection writeback is healthy."})",
            $"Acceptance contract ingress policy: {acceptanceContractIngressPolicy["policy_summary"]?.GetValue<string>() ?? "(none)"}",
            $"Session Gateway assist: {sessionGatewayGovernanceAssist["overall_posture"]?.GetValue<string>() ?? "(none)"}",
            $"Session Gateway top pressure: {sessionGatewayTopPressure["pressure_kind"]?.GetValue<string>() ?? "(none)"} [{sessionGatewayTopPressure["level"]?.GetValue<string>() ?? "none"}]",
            $"Session Gateway next action: {sessionGatewayGovernanceAssist["recommended_next_action"]?.GetValue<string>() ?? "(none)"}",
            "Accepted operations:",
        };

        lines.AddRange(acceptedOperations.Count == 0
            ? ["  - (none)"]
            : acceptedOperations.Select(node =>
            {
                var operation = node?.AsObject() ?? new JsonObject();
                return $"  - {operation["command"]?.GetValue<string>() ?? "(unknown)"} [{operation["progress_marker"]?.GetValue<string>() ?? "(none)"}] completed={operation["completed"]?.GetValue<bool>() ?? false} | updated={operation["updated_at"]?.ToString() ?? "(none)"}";
            }));

        return lines;
    }

    public JsonObject BuildDashboardSummary(LocalHostState hostState)
    {
        var graph = services.TaskGraphService.Load();
        var completedTaskIds = graph.CompletedTaskIds();
        var session = services.DevLoopService.GetSession();
        var platformStatus = services.OperatorApiService.GetPlatformStatus();
        var repoId = platformStatus.Repos.FirstOrDefault()?.RepoId ?? "local-repo";
        var workerSelection = services.OperatorApiService.GetWorkerSelection(repoId, null);
        var blockedReasons = BuildBlockedReasonCounts(graph);
        var cardDrafts = services.PlanningDraftService.ListCardDrafts();
        var taskGraphDrafts = services.PlanningDraftService.ListTaskGraphDrafts();
        var dispatch = services.DispatchProjectionService.Build(graph, session, services.SystemConfig.MaxParallelTasks);
        var interaction = services.InteractionLayerService.GetSnapshot(session);
        var incidents = services.OperatorApiService.GetRuntimeIncidents();
        // Dashboard text/html is a read-side surface and should not block on live provider probes.
        var providerHealth = services.OperatorApiService.GetWorkerHealth(refresh: false);
        var actorSessions = services.OperatorApiService.GetActorSessions();
        var ownershipBindings = services.OperatorApiService.GetOwnershipBindings();
        var operatorOsEvents = services.OperatorApiService.GetOperatorOsEvents();
        var operational = services.OperationalSummaryService.Build(refreshProviderHealth: false);
        var sessionGatewayGovernanceAssist = services.PlatformDashboardService.BuildSessionGatewayGovernanceAssistSurface();
        var acceptanceContractIngressPolicy = services.PlatformDashboardService.BuildAcceptanceContractIngressPolicySurface();
        var agentWorkingModes = services.PlatformDashboardService.BuildAgentWorkingModesSurface();
        var formalPlanningPosture = services.PlatformDashboardService.BuildFormalPlanningPostureSurface();
        var vendorNativeAcceleration = services.PlatformDashboardService.BuildVendorNativeAccelerationSurface();
        var projectionHealth = new MarkdownProjectionHealthService(services.Paths).Load();
        var governance = services.GovernanceReportingService.Build();
        var plannerEmergence = new PlannerEmergenceService(services.Paths, services.TaskGraphService, services.ExecutionRunService).BuildProjection();
        var rehydration = hostState.RehydrationSummary;
        var hostSession = new HostSessionService(services.Paths).Load();
        var runtimeManifest = new RuntimeManifestService(services.Paths).Load();
        var runtimeHealth = new RuntimeHealthCheckService(services.Paths, services.TaskGraphService).Evaluate();
        var runDrilldown = BuildRunDrilldown(5);

        return new JsonObject
        {
            ["host_running"] = true,
            ["stage"] = RuntimeStageInfo.CurrentStage,
            ["uptime_seconds"] = Math.Max(0, (long)(DateTimeOffset.UtcNow - hostState.StartedAt).TotalSeconds),
            ["dashboard_url"] = hostState.DashboardUrl,
            ["workbench_url"] = hostState.WorkbenchUrl,
            ["current_actionability"] = session is null ? "none" : RuntimeActionabilitySemantics.Describe(session.CurrentActionability),
            ["rehydration"] = new JsonObject
            {
                ["rehydrated"] = rehydration.Rehydrated,
                ["stale_marker_count"] = rehydration.StaleMarkerCount,
                ["paused_runtime_count"] = rehydration.PausedRuntimeCount,
                ["pending_approval_count"] = rehydration.PendingApprovalCount,
                ["summary"] = rehydration.Summary,
                ["cleanup_actions"] = ToJsonArray(rehydration.CleanupActions),
            },
            ["host_session"] = new JsonObject
            {
                ["session_id"] = hostSession?.SessionId,
                ["status"] = hostSession?.Status.ToString().ToLowerInvariant(),
                ["control_state"] = hostSession?.ControlState.ToString().ToLowerInvariant(),
                ["last_control_action"] = hostSession?.LastControlAction.ToString().ToLowerInvariant(),
                ["last_control_reason"] = hostSession?.LastControlReason,
                ["last_control_at"] = hostSession?.LastControlAt,
                ["host_id"] = hostSession?.HostId,
                ["repo_root"] = hostSession?.RepoRoot,
                ["base_url"] = hostSession?.BaseUrl,
                ["started_at"] = hostSession?.StartedAt,
                ["ended_at"] = hostSession?.EndedAt,
                ["attached_repo_count"] = hostSession?.AttachedRepos.Count ?? 0,
                ["attached_repos"] = hostSession is null
                    ? new JsonArray()
                    : new JsonArray(hostSession.AttachedRepos.Select(item => (JsonNode)new JsonObject
                    {
                        ["repo_id"] = item.RepoId,
                        ["repo_path"] = item.RepoPath,
                        ["client_repo_root"] = item.ClientRepoRoot,
                        ["attach_mode"] = item.AttachMode,
                        ["runtime_health"] = item.RuntimeHealth,
                        ["attached_at"] = item.AttachedAt,
                    }).ToArray()),
            },
            ["host_control"] = new JsonObject
            {
                ["state"] = hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running",
                ["last_action"] = hostSession?.LastControlAction.ToString().ToLowerInvariant() ?? "started",
                ["reason"] = hostSession?.LastControlReason ?? "Resident host session started.",
                ["at"] = hostSession?.LastControlAt,
                ["actions"] = new JsonObject
                {
                    ["pause"] = "host pause <reason...>",
                    ["resume"] = "host resume <reason...>",
                    ["stop"] = "host stop <reason...>",
                },
            },
            ["accepted_operations"] = BuildAcceptedOperationsNode(),
            ["runtime_truth"] = new JsonObject
            {
                ["manifest_path"] = new RuntimeManifestService(services.Paths).ManifestPath,
                ["manifest"] = runtimeManifest is null
                    ? null
                    : new JsonObject
                    {
                        ["repo_id"] = runtimeManifest.RepoId,
                        ["repo_path"] = runtimeManifest.RepoPath,
                        ["git_root"] = runtimeManifest.GitRoot,
                        ["active_branch"] = runtimeManifest.ActiveBranch,
                        ["runtime_version"] = runtimeManifest.RuntimeVersion,
                        ["client_version"] = runtimeManifest.ClientVersion,
                        ["host_session_id"] = runtimeManifest.HostSessionId,
                        ["runtime_status"] = runtimeManifest.RuntimeStatus,
                        ["repo_summary"] = runtimeManifest.RepoSummary,
                        ["state"] = runtimeManifest.State.ToString().ToLowerInvariant(),
                        ["created_at"] = runtimeManifest.CreatedAt,
                        ["last_attached_at"] = runtimeManifest.LastAttachedAt,
                        ["last_repair_at"] = runtimeManifest.LastRepairAt,
                    },
                ["health"] = new JsonObject
                {
                    ["state"] = runtimeHealth.State.ToString().ToLowerInvariant(),
                    ["summary"] = runtimeHealth.Summary,
                    ["suggested_action"] = runtimeHealth.SuggestedAction,
                    ["missing_directories"] = ToJsonArray(runtimeHealth.MissingDirectories),
                    ["interrupted_tasks"] = ToJsonArray(runtimeHealth.InterruptedTaskIds),
                    ["dangling_artifacts"] = ToJsonArray(runtimeHealth.DanglingArtifactPaths),
                },
            },
            ["projection_writeback"] = new JsonObject
            {
                ["state"] = projectionHealth.State,
                ["summary"] = projectionHealth.Summary,
                ["consecutive_failure_count"] = projectionHealth.ConsecutiveFailureCount,
                ["updated_at"] = projectionHealth.UpdatedAt,
                ["last_success_at"] = projectionHealth.LastSuccessAt,
                ["last_failure_at"] = projectionHealth.LastFailureAt,
                ["last_failure"] = projectionHealth.LastFailure,
                ["affected_targets"] = ToJsonArray(projectionHealth.AffectedTargets),
            },
            ["acceptance_contract_ingress_policy"] = new JsonObject
            {
                ["overall_posture"] = acceptanceContractIngressPolicy.OverallPosture,
                ["planning_truth_mutation_policy"] = acceptanceContractIngressPolicy.PlanningTruthMutationPolicy,
                ["execution_dispatch_policy"] = acceptanceContractIngressPolicy.ExecutionDispatchPolicy,
                ["policy_summary"] = acceptanceContractIngressPolicy.PolicySummary,
                ["document_path"] = acceptanceContractIngressPolicy.PolicyDocumentPath,
                ["recommended_next_action"] = acceptanceContractIngressPolicy.RecommendedNextAction,
            },
            ["agent_working_modes"] = new JsonObject
            {
                ["overall_posture"] = agentWorkingModes.OverallPosture,
                ["current_mode"] = agentWorkingModes.CurrentMode,
                ["current_mode_summary"] = agentWorkingModes.CurrentModeSummary,
                ["strongest_runtime_supported_mode"] = agentWorkingModes.StrongestRuntimeSupportedMode,
                ["external_agent_recommended_mode"] = agentWorkingModes.ExternalAgentRecommendedMode,
                ["external_agent_recommendation_posture"] = agentWorkingModes.ExternalAgentRecommendationPosture,
                ["external_agent_recommendation_summary"] = agentWorkingModes.ExternalAgentRecommendationSummary,
                ["external_agent_recommended_action"] = agentWorkingModes.ExternalAgentRecommendedAction,
                ["external_agent_constraint_tier"] = agentWorkingModes.ExternalAgentConstraintTier,
                ["external_agent_constraint_summary"] = agentWorkingModes.ExternalAgentConstraintSummary,
                ["external_agent_stronger_mode_blocker_count"] = agentWorkingModes.ExternalAgentStrongerModeBlockerCount,
                ["external_agent_first_stronger_mode_blocker_id"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId,
                ["external_agent_first_stronger_mode_blocker_target_mode"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerTargetMode,
                ["external_agent_first_stronger_mode_blocker_required_action"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction,
                ["external_agent_first_stronger_mode_blocker_constraint_class"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerConstraintClass,
                ["external_agent_first_stronger_mode_blocker_enforcement_level"] = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerEnforcementLevel,
                ["planning_coupling_posture"] = agentWorkingModes.PlanningCouplingPosture,
                ["planning_coupling_summary"] = agentWorkingModes.PlanningCouplingSummary,
                ["plan_handle"] = agentWorkingModes.PlanHandle,
                ["planning_card_id"] = agentWorkingModes.PlanningCardId,
                ["managed_workspace_posture"] = agentWorkingModes.ManagedWorkspacePosture,
                ["path_policy_enforcement_state"] = agentWorkingModes.PathPolicyEnforcementState,
                ["mode_e_operational_activation_state"] = agentWorkingModes.ModeEOperationalActivationState,
                ["mode_e_operational_activation_summary"] = agentWorkingModes.ModeEOperationalActivationSummary,
                ["mode_e_activation_task_id"] = agentWorkingModes.ModeEActivationTaskId,
                ["mode_e_activation_result_return_channel"] = agentWorkingModes.ModeEActivationResultReturnChannel,
                ["mode_e_activation_commands"] = ToJsonArray(agentWorkingModes.ModeEActivationCommands),
                ["mode_e_activation_recommended_next_action"] = agentWorkingModes.ModeEActivationRecommendedNextAction,
                ["mode_e_activation_blocking_check_count"] = agentWorkingModes.ModeEActivationBlockingCheckCount,
                ["mode_e_activation_first_blocking_check_id"] = agentWorkingModes.ModeEActivationFirstBlockingCheckId,
                ["mode_e_activation_first_blocking_check_summary"] = agentWorkingModes.ModeEActivationFirstBlockingCheckSummary,
                ["mode_e_activation_first_blocking_check_required_action"] = agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction,
                ["mode_e_activation_playbook_summary"] = agentWorkingModes.ModeEActivationPlaybookSummary,
                ["mode_e_activation_playbook_step_count"] = agentWorkingModes.ModeEActivationPlaybookStepCount,
                ["mode_e_activation_first_playbook_step_command"] = agentWorkingModes.ModeEActivationFirstPlaybookStepCommand,
                ["mode_e_activation_first_playbook_step_summary"] = agentWorkingModes.ModeEActivationFirstPlaybookStepSummary,
                ["recommended_next_action"] = agentWorkingModes.RecommendedNextAction,
            },
            ["formal_planning_posture"] = new JsonObject
            {
                ["overall_posture"] = formalPlanningPosture.OverallPosture,
                ["intent_state"] = formalPlanningPosture.IntentState,
                ["guided_planning_posture"] = formalPlanningPosture.GuidedPlanningPosture,
                ["formal_planning_state"] = formalPlanningPosture.FormalPlanningState,
                ["formal_planning_entry_trigger_state"] = formalPlanningPosture.FormalPlanningEntryTriggerState,
                ["formal_planning_entry_command"] = formalPlanningPosture.FormalPlanningEntryCommand,
                ["formal_planning_entry_recommended_next_action"] = formalPlanningPosture.FormalPlanningEntryRecommendedNextAction,
                ["formal_planning_entry_summary"] = formalPlanningPosture.FormalPlanningEntrySummary,
                ["active_planning_slot_state"] = formalPlanningPosture.ActivePlanningSlotState,
                ["active_planning_slot_can_initialize"] = formalPlanningPosture.ActivePlanningSlotCanInitialize,
                ["active_planning_slot_conflict_reason"] = formalPlanningPosture.ActivePlanningSlotConflictReason,
                ["active_planning_slot_remediation_action"] = formalPlanningPosture.ActivePlanningSlotRemediationAction,
                ["planning_card_invariant_state"] = formalPlanningPosture.PlanningCardInvariantState,
                ["planning_card_invariant_can_export_governed_truth"] = formalPlanningPosture.PlanningCardInvariantCanExportGovernedTruth,
                ["planning_card_invariant_summary"] = formalPlanningPosture.PlanningCardInvariantSummary,
                ["planning_card_invariant_remediation_action"] = formalPlanningPosture.PlanningCardInvariantRemediationAction,
                ["planning_card_invariant_block_count"] = formalPlanningPosture.PlanningCardInvariantBlockCount,
                ["planning_card_invariant_violation_count"] = formalPlanningPosture.PlanningCardInvariantViolationCount,
                ["active_planning_card_fill_state"] = formalPlanningPosture.ActivePlanningCardFillState,
                ["active_planning_card_fill_completion_posture"] = formalPlanningPosture.ActivePlanningCardFillCompletionPosture,
                ["active_planning_card_fill_ready_for_recommended_export"] = formalPlanningPosture.ActivePlanningCardFillReadyForRecommendedExport,
                ["active_planning_card_fill_summary"] = formalPlanningPosture.ActivePlanningCardFillSummary,
                ["active_planning_card_fill_recommended_next_action"] = formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction,
                ["active_planning_card_fill_next_missing_field_path"] = formalPlanningPosture.ActivePlanningCardFillNextMissingFieldPath,
                ["active_planning_card_fill_required_field_count"] = formalPlanningPosture.ActivePlanningCardFillRequiredFieldCount,
                ["active_planning_card_fill_missing_required_field_count"] = formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount,
                ["active_planning_card_fill_missing_field_paths"] = ToJsonArray(formalPlanningPosture.ActivePlanningCardFillMissingFieldPaths),
                ["current_mode"] = formalPlanningPosture.CurrentMode,
                ["planning_coupling_posture"] = formalPlanningPosture.PlanningCouplingPosture,
                ["plan_handle"] = formalPlanningPosture.PlanHandle,
                ["planning_card_id"] = formalPlanningPosture.PlanningCardId,
                ["packet_available"] = formalPlanningPosture.PacketAvailable,
                ["packet_summary"] = formalPlanningPosture.PacketSummary,
                ["recommended_next_action"] = formalPlanningPosture.RecommendedNextAction,
                ["managed_workspace_posture"] = formalPlanningPosture.ManagedWorkspacePosture,
                ["path_policy_enforcement_state"] = formalPlanningPosture.PathPolicyEnforcementState,
                ["plan_required_block_count"] = formalPlanningPosture.PlanRequiredBlockCount,
                ["workspace_required_block_count"] = formalPlanningPosture.WorkspaceRequiredBlockCount,
                ["mode_execution_entry_first_blocked_task_id"] = formalPlanningPosture.ModeExecutionEntryFirstBlockedTaskId,
                ["mode_execution_entry_first_blocking_check_id"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckId,
                ["mode_execution_entry_first_blocking_check_summary"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckSummary,
                ["mode_execution_entry_first_blocking_check_required_action"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckRequiredAction,
                ["mode_execution_entry_first_blocking_check_required_command"] = formalPlanningPosture.ModeExecutionEntryFirstBlockingCheckRequiredCommand,
                ["mode_execution_entry_recommended_next_action"] = formalPlanningPosture.ModeExecutionEntryRecommendedNextAction,
                ["mode_execution_entry_recommended_next_command"] = formalPlanningPosture.ModeExecutionEntryRecommendedNextCommand,
                ["missing_prerequisites"] = ToJsonArray(formalPlanningPosture.MissingPrerequisites),
            },
            ["vendor_native_acceleration"] = new JsonObject
            {
                ["overall_posture"] = vendorNativeAcceleration.OverallPosture,
                ["current_mode"] = vendorNativeAcceleration.CurrentMode,
                ["planning_coupling_posture"] = vendorNativeAcceleration.PlanningCouplingPosture,
                ["formal_planning_posture"] = vendorNativeAcceleration.FormalPlanningPosture,
                ["plan_handle"] = vendorNativeAcceleration.PlanHandle,
                ["planning_card_id"] = vendorNativeAcceleration.PlanningCardId,
                ["managed_workspace_posture"] = vendorNativeAcceleration.ManagedWorkspacePosture,
                ["portable_foundation_summary"] = vendorNativeAcceleration.PortableFoundationSummary,
                ["codex_reinforcement_state"] = vendorNativeAcceleration.CodexReinforcementState,
                ["claude_reinforcement_state"] = vendorNativeAcceleration.ClaudeReinforcementState,
                ["recommended_next_action"] = vendorNativeAcceleration.RecommendedNextAction,
            },
            ["session_gateway_governance_assist"] = BuildSessionGatewayGovernanceAssistDashboardNode(sessionGatewayGovernanceAssist),
            ["planner"] = new JsonObject
            {
                ["state"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState),
                ["wake_reason"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason),
                ["pending_wake_count"] = session?.PendingPlannerWakeSignals.Count ?? 0,
                ["last_consumed_wake"] = session?.LastConsumedPlannerWakeSummary,
                ["sleep_reason"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason),
                ["lease_id"] = session?.PlannerLeaseId,
                ["lease_active"] = session?.PlannerLeaseActive ?? false,
                ["lease_mode"] = session?.PlannerLeaseMode.ToString(),
                ["lease_owner"] = session?.PlannerLeaseOwner,
                ["last_planning_run"] = session?.PlannerLifecycleReason ?? "(none)",
            },
            ["interaction"] = new JsonObject
            {
                ["protocol_mode"] = interaction.ProtocolMode,
                ["conversation_phase"] = interaction.Protocol.CurrentPhase.ToString().ToLowerInvariant(),
                ["intent_state"] = interaction.Intent.State.ToString().ToLowerInvariant(),
                ["prompt_kernel"] = $"{interaction.PromptKernel.KernelId}@{interaction.PromptKernel.Version}",
                ["prompt_template"] = $"{interaction.ActiveTemplate.TemplateId}@{interaction.ActiveTemplate.Version}",
                ["project_understanding_state"] = interaction.ProjectUnderstanding.State.ToString().ToLowerInvariant(),
                ["project_understanding_action"] = interaction.ProjectUnderstanding.Action,
                ["project_summary"] = interaction.ProjectUnderstanding.Summary,
                ["next_action"] = interaction.RecommendedNextAction,
            },
            ["workers"] = new JsonObject
            {
                ["worker_count"] = platformStatus.WorkerNodeCount,
                ["active_worker_count"] = session?.ActiveWorkerCount ?? 0,
                ["active_tasks"] = ToJsonArray(session?.ActiveTaskIds ?? Array.Empty<string>()),
                ["leases"] = platformStatus.ActiveLeaseCount,
                ["selection_backend"] = workerSelection.SelectedBackendId ?? "(none)",
                ["selection_profile"] = workerSelection.RequestedTrustProfileId,
                ["selection_summary"] = workerSelection.Summary,
                ["provider_health"] = new JsonArray(providerHealth.Take(5).Select(record => new JsonObject
                {
                    ["backend_id"] = record.BackendId,
                    ["state"] = record.State.ToString(),
                    ["latency_ms"] = record.LatencyMs,
                    ["summary"] = record.Summary,
                }).ToArray()),
            },
            ["permissions"] = BuildWorkerApprovals(),
            ["operational"] = JsonNode.Parse(JsonSerializer.Serialize(operational, JsonOptions)),
            ["attached_repos"] = new JsonArray(platformStatus.Repos.Select(item =>
            {
                var manifest = new RuntimeManifestService(ControlPlanePaths.FromRepoRoot(item.RepoPath)).Load();
                return (JsonNode)new JsonObject
                {
                    ["repo_id"] = item.RepoId,
                    ["repo_path"] = item.RepoPath,
                    ["runtime_status"] = item.RuntimeStatus,
                    ["actionability"] = item.Actionability,
                    ["branch"] = manifest?.ActiveBranch,
                    ["runtime_health"] = manifest?.State.ToString().ToLowerInvariant(),
                    ["host_session_id"] = manifest?.HostSessionId,
                };
            }).ToArray()),
            ["actor_sessions"] = new JsonObject
            {
                ["total"] = actorSessions.Count,
                ["active"] = actorSessions.Count(item => item.State == ActorSessionState.Active),
                ["blocked"] = actorSessions.Count(item => item.State == ActorSessionState.Blocked),
                ["sessions"] = new JsonArray(actorSessions.Take(8).Select(item => new JsonObject
                {
                    ["actor_session_id"] = item.ActorSessionId,
                    ["kind"] = item.Kind.ToString(),
                    ["state"] = item.State.ToString(),
                    ["actor_identity"] = item.ActorIdentity,
                    ["process_id"] = item.ProcessId,
                    ["process_started_at"] = item.ProcessStartedAt,
                    ["process_tracking_status"] = ActorSessionLivenessRules.ResolveProcessTrackingStatus(item),
                    ["ownership_scope"] = item.CurrentOwnershipScope?.ToString(),
                    ["ownership_target_id"] = item.CurrentOwnershipTargetId,
                }).ToArray()),
            },
            ["ownership"] = new JsonObject
            {
                ["total"] = ownershipBindings.Count,
                ["bindings"] = new JsonArray(ownershipBindings.Take(8).Select(item => new JsonObject
                {
                    ["binding_id"] = item.BindingId,
                    ["scope"] = item.Scope.ToString(),
                    ["target_id"] = item.TargetId,
                    ["owner"] = $"{item.OwnerKind}:{item.OwnerIdentity}",
                    ["reason"] = item.Reason,
                }).ToArray()),
            },
            ["cards"] = BuildCardMetrics(graph, cardDrafts),
            ["drafts"] = new JsonObject
            {
                ["card_drafts"] = cardDrafts.Count(item => item.Status == CardLifecycleState.Draft),
                ["taskgraph_drafts"] = taskGraphDrafts.Count(item => item.Status == PlanningDraftStatus.Draft),
                ["approved_taskgraph_drafts"] = taskGraphDrafts.Count(item => item.Status == PlanningDraftStatus.Approved),
            },
            ["planner_emergence"] = new JsonObject
            {
                ["replan_required_tasks"] = plannerEmergence.ReplanRequiredTaskCount,
                ["draft_suggested_tasks"] = plannerEmergence.DraftSuggestedTaskCount,
                ["planning_signals"] = plannerEmergence.PlanningSignalCount,
                ["execution_memory_records"] = plannerEmergence.ExecutionMemoryRecordCount,
            },
            ["tasks"] = new JsonObject
            {
                ["total"] = graph.Tasks.Count,
                ["ready"] = dispatch.ReadyTaskCount,
                ["acceptance_contract_gap_count"] = dispatch.AcceptanceContractGapCount,
                ["plan_required_block_count"] = dispatch.PlanRequiredBlockCount,
                ["workspace_required_block_count"] = dispatch.WorkspaceRequiredBlockCount,
                ["running"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Running),
                ["approval_wait"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.ApprovalWait),
                ["review"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
                ["blocked"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Blocked),
                ["failed"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Failed),
                ["completed"] = graph.Tasks.Values.Count(task => task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded),
            },
            ["dispatch"] = new JsonObject
            {
                ["state"] = dispatch.State,
                ["summary"] = dispatch.Summary,
                ["idle_reason_code"] = dispatch.IdleReason,
                ["idle_reason"] = services.DispatchProjectionService.DescribeIdleReason(dispatch.IdleReason),
                ["next_task_id"] = dispatch.NextTaskId,
                ["ready_task_count"] = dispatch.ReadyTaskCount,
                ["acceptance_contract_gap_count"] = dispatch.AcceptanceContractGapCount,
                ["plan_required_block_count"] = dispatch.PlanRequiredBlockCount,
                ["workspace_required_block_count"] = dispatch.WorkspaceRequiredBlockCount,
                ["first_blocked_task_id"] = dispatch.FirstBlockedTaskId,
                ["first_blocking_check_id"] = dispatch.FirstBlockingCheckId,
                ["first_blocking_check_summary"] = dispatch.FirstBlockingCheckSummary,
                ["first_blocking_check_required_action"] = dispatch.FirstBlockingCheckRequiredAction,
                ["first_blocking_check_required_command"] = dispatch.FirstBlockingCheckRequiredCommand,
                ["recommended_next_action"] = dispatch.RecommendedNextAction,
                ["recommended_next_command"] = dispatch.RecommendedNextCommand,
                ["active_worker_count"] = dispatch.ActiveWorkerCount,
                ["max_worker_count"] = dispatch.MaxWorkerCount,
                ["auto_continue_on_approve"] = dispatch.AutoContinueOnApprove,
            },
            ["run_drilldown"] = runDrilldown,
            ["blocked"] = blockedReasons,
            ["incidents"] = new JsonObject
            {
                ["recent_total"] = incidents.Count,
                ["recent"] = new JsonArray(incidents.Take(10).Select(incident => new JsonObject
                {
                    ["incident_type"] = incident.IncidentType.ToString(),
                    ["task_id"] = incident.TaskId,
                    ["backend_id"] = incident.BackendId,
                    ["recovery_action"] = incident.RecoveryAction.ToString(),
                    ["summary"] = incident.Summary,
                }).ToArray()),
            },
            ["operator_os_events"] = new JsonObject
            {
                ["recent_total"] = operatorOsEvents.Count,
                ["recent"] = new JsonArray(operatorOsEvents.Take(10).Select(item => new JsonObject
                {
                    ["event_kind"] = item.EventKind.ToString(),
                    ["actor"] = item.ActorIdentity,
                    ["task_id"] = item.TaskId,
                    ["run_id"] = item.RunId,
                    ["summary"] = item.Summary,
                }).ToArray()),
            },
            ["governance_report"] = JsonNode.Parse(JsonSerializer.Serialize(governance, JsonOptions)),
        };
    }

    public string RenderDashboardHtml(LocalHostState hostState)
    {
        var summary = BuildDashboardSummary(hostState);
        var graph = services.TaskGraphService.Load();
        var latestCards = graph.Cards.OrderBy(id => id, StringComparer.Ordinal).Take(12).ToArray();
        var latestTasks = graph.ListTasks().Take(12).ToArray();

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>CARVES Local Dashboard</title>
  <meta http-equiv="refresh" content="3">
  <style>
    body { font-family: Consolas, "Courier New", monospace; margin: 24px; background: #f3f1eb; color: #1f1b16; }
    h1, h2 { margin-bottom: 8px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
    .panel { background: #fffdf7; border: 1px solid #d8d0c2; padding: 16px; border-radius: 8px; }
    ul { padding-left: 20px; }
    a { color: #0a5c6b; text-decoration: none; }
    code, pre { white-space: pre-wrap; }
  </style>
</head>
<body>
  <h1>CARVES Local Developer Dashboard</h1>
  <p>Stage: {{summary["stage"]}} | Uptime: {{summary["uptime_seconds"]}}s | Actionability: {{summary["current_actionability"]}}</p>
    <div class="grid">
    <div class="panel">
      <h2>Host Session</h2>
      <pre>{{summary["host_session"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Host Control</h2>
      <pre>{{summary["host_control"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Runtime Truth</h2>
      <pre>{{summary["runtime_truth"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Projection Writeback</h2>
      <pre>{{summary["projection_writeback"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Agent Working Modes</h2>
      <pre>{{summary["agent_working_modes"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Formal Planning Posture</h2>
      <pre>{{summary["formal_planning_posture"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Vendor-Native Acceleration</h2>
      <pre>{{summary["vendor_native_acceleration"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Session Gateway Assist</h2>
      <pre>{{summary["session_gateway_governance_assist"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Interaction</h2>
      <pre>{{summary["interaction"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Workers</h2>
      <pre>{{summary["workers"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Planner</h2>
      <pre>{{summary["planner"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Permissions</h2>
      <pre>{{summary["permissions"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Actor Sessions</h2>
      <pre>{{summary["actor_sessions"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Ownership</h2>
      <pre>{{summary["ownership"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Cards</h2>
      <pre>{{summary["cards"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Attached Repos</h2>
      <pre>{{summary["attached_repos"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Tasks</h2>
      <pre>{{summary["tasks"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Planner Emergence</h2>
      <pre>{{summary["planner_emergence"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Run Drilldown</h2>
      <pre>{{summary["run_drilldown"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Blocked</h2>
      <pre>{{summary["blocked"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Operator OS Events</h2>
      <pre>{{summary["operator_os_events"]!.ToJsonString(JsonOptions)}}</pre>
    </div>
    <div class="panel">
      <h2>Cards</h2>
      <ul>
        {{string.Join(Environment.NewLine, latestCards.Select(cardId => $"<li><a href=\"/inspect/card/{cardId}\">{cardId}</a></li>"))}}
      </ul>
    </div>
    <div class="panel">
      <h2>Tasks</h2>
      <ul>
        {{string.Join(Environment.NewLine, latestTasks.Select(task => $"<li><a href=\"/inspect/task/{task.TaskId}\">{task.TaskId}</a> [{task.Status}]</li>"))}}
      </ul>
    </div>
  </div>
</body>
</html>
""";
    }

    private static JsonObject BuildSessionGatewayGovernanceAssistDashboardNode(RuntimeSessionGatewayGovernanceAssistSurface surface)
    {
        var topPressure = surface.ChangePressures.FirstOrDefault();
        var topCandidate = surface.DecompositionCandidates.FirstOrDefault();

        return new JsonObject
        {
            ["overall_posture"] = surface.OverallPosture,
            ["repeatability_posture"] = surface.RepeatabilityPosture,
            ["is_valid"] = surface.IsValid,
            ["acceptance_contract_binding_gap_count"] = surface.AcceptanceContractBindingGapCount,
            ["review_evidence_blocked_count"] = surface.ReviewEvidenceBlockedCount,
            ["recommended_next_action"] = surface.RecommendedNextAction,
            ["highest_priority_pressure"] = topPressure is null
                ? null
                : new JsonObject
                {
                    ["pressure_kind"] = topPressure.PressureKind,
                    ["level"] = topPressure.Level,
                    ["summary"] = topPressure.Summary,
                },
            ["highest_priority_candidate"] = topCandidate is null
                ? null
                : new JsonObject
                {
                    ["candidate_id"] = topCandidate.CandidateId,
                    ["title"] = topCandidate.Title,
                    ["blocking_state"] = topCandidate.BlockingState,
                    ["suggested_action"] = topCandidate.SuggestedAction,
                },
        };
    }
}
