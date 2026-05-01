using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildWorkerAutomationReadiness(string? repoId = null, bool refreshWorkerHealth = false)
    {
        var resolvedRepoId = ResolveRepoId(repoId);
        var graph = services.TaskGraphService.Load();
        var session = services.DevLoopService.GetSession();
        var dispatch = services.DispatchProjectionService.Build(graph, session, services.SystemConfig.MaxParallelTasks);
        var selection = ResolveWorkerAutomationSelection(resolvedRepoId, dispatch.NextTaskId, out var selectionError);
        var providerHealth = selectionError is null
            ? services.OperatorApiService.GetWorkerHealth(refreshWorkerHealth, selection.SelectedBackendId)
            : Array.Empty<ProviderHealthRecord>();
        var selectedHealth = ResolveSelectedHealth(providerHealth, selection.SelectedBackendId);
        var selectedBackend = ResolveSelectedBackend(services.OperatorApiService.GetWorkerProviders(), selection.SelectedBackendId);
        var selectedCandidate = selection.Candidates.FirstOrDefault(candidate => candidate.Selected);
        var workerRuntimeStatus = ResolveWorkerRuntimeStatus(selection, selectedHealth, selectedCandidate, selectedBackend);
        var selectedBackendExternalAppCli = IsExternalAppCliWorkerBackend(selectedBackend);
        var roleGovernancePolicy = services.RuntimePolicyBundleService.LoadRoleGovernancePolicy();
        var roleModeGate = RuntimeRoleModeExecutionGate.EvaluateSchedulerAutoDispatch(roleGovernancePolicy);
        var reviewGate = BuildReviewGateAutomationReadiness(graph.ListTasks());
        var hostRuntimeReady = true;
        const string hostReadiness = "host_control_plane_surface_responded";
        var dispatchable = string.Equals(dispatch.State, "dispatchable", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(dispatch.NextTaskId);
        var workerRuntimeAvailable = selectedBackendExternalAppCli
                                     && string.Equals(workerRuntimeStatus, "available", StringComparison.Ordinal);
        var reviewGateClear = reviewGate.BlockedCount == 0;
        var automationCanRunNow = roleModeGate.Allowed
                                  && hostRuntimeReady
                                  && dispatchable
                                  && workerRuntimeAvailable
                                  && reviewGateClear;
        var blockers = BuildWorkerAutomationBlockers(
            roleModeGate,
            hostRuntimeReady,
            dispatchable,
            workerRuntimeAvailable,
            reviewGateClear,
            hostReadiness,
            dispatch,
            selection,
            selectedBackend,
            selectedBackendExternalAppCli,
            selectionError,
            workerRuntimeStatus,
            reviewGate);
        var readinessStatus = automationCanRunNow ? "ready" : "blocked";

        return new JsonObject
        {
            ["kind"] = "worker_automation_readiness",
            ["repo_id"] = resolvedRepoId,
            ["readiness_type"] = "worker_automation_runtime_readiness",
            ["readiness_claim"] = "host_dispatch_runtime_readiness_only",
            ["not_role_binding_readiness"] = true,
            ["role_binding_surface_ref"] = "agent query actor-role-readiness",
            ["status"] = readinessStatus,
            ["automation_can_run_now"] = automationCanRunNow,
            ["role_mode_gate"] = BuildRoleModeGateSurface(roleGovernancePolicy, roleModeGate),
            ["run_eligibility"] = BuildWorkerAutomationRunEligibility(
                automationCanRunNow,
                roleModeGate,
                hostRuntimeReady,
                dispatchable,
                workerRuntimeAvailable,
                reviewGateClear,
                hostReadiness,
                dispatch,
                selection,
                selectedBackend,
                selectedBackendExternalAppCli,
                selectionError,
                workerRuntimeStatus,
                reviewGate,
                blockers),
            ["schedule_tick_plan"] = BuildWorkerAutomationScheduleTickPlan(
                resolvedRepoId,
                automationCanRunNow,
                roleGovernancePolicy,
                roleModeGate,
                dispatch,
                blockers),
            ["role_binding_readiness"] = new JsonObject
            {
                ["checked"] = false,
                ["status"] = "not_evaluated",
                ["surface_ref"] = "agent query actor-role-readiness",
                ["summary"] = "Role-binding readiness is intentionally separate from worker automation run eligibility.",
            },
            ["host_health"] = new JsonObject
            {
                ["checked"] = true,
                ["eligible"] = hostRuntimeReady,
                ["reachable"] = hostRuntimeReady,
                ["readiness"] = hostReadiness,
                ["source"] = "host_control_plane_surface",
                ["summary"] = "Host control-plane surface responded to this readiness query.",
            },
            ["dispatchable_task"] = new JsonObject
            {
                ["checked"] = true,
                ["available"] = dispatchable,
                ["dispatch_state"] = dispatch.State,
                ["idle_reason"] = dispatch.IdleReason,
                ["summary"] = dispatch.Summary,
                ["next_task_id"] = dispatch.NextTaskId,
                ["ready_task_count"] = dispatch.ReadyTaskCount,
                ["active_worker_count"] = dispatch.ActiveWorkerCount,
                ["max_worker_count"] = dispatch.MaxWorkerCount,
                ["first_blocked_task_id"] = dispatch.FirstBlockedTaskId,
                ["first_blocking_check_id"] = dispatch.FirstBlockingCheckId,
                ["first_blocking_check_summary"] = dispatch.FirstBlockingCheckSummary,
                ["recommended_next_action"] = dispatch.RecommendedNextAction,
                ["recommended_next_command"] = dispatch.RecommendedNextCommand,
            },
            ["worker_runtime"] = new JsonObject
            {
                ["checked"] = true,
                ["available"] = workerRuntimeAvailable,
                ["status"] = workerRuntimeStatus,
                ["selection_allowed"] = selection.Allowed,
                ["selected_backend_id"] = selection.SelectedBackendId,
                ["selected_provider_id"] = selection.SelectedProviderId,
                ["selected_adapter_id"] = selection.SelectedAdapterId,
                ["selected_model_id"] = selection.SelectedModelId,
                ["selected_protocol_family"] = selectedBackend?.ProtocolFamily,
                ["selected_request_family"] = selectedBackend?.RequestFamily,
                ["selected_backend_external_app_cli"] = selectedBackendExternalAppCli,
                ["external_app_cli_required"] = true,
                ["sdk_api_worker_boundary"] = "closed_until_separate_governed_activation",
                ["selection_error"] = selectionError,
                ["reason_code"] = selection.ReasonCode,
                ["summary"] = selection.Summary,
                ["provider_health_refreshed"] = refreshWorkerHealth,
                ["selected_health_state"] = selectedHealth is null
                    ? selectedCandidate?.HealthState ?? "not_recorded"
                    : selectedHealth.State.ToString().ToLowerInvariant(),
                ["selected_health_summary"] = selectedHealth?.Summary,
                ["selected_health_checked_at"] = selectedHealth?.CheckedAt,
            },
            ["review_gate"] = new JsonObject
            {
                ["checked"] = true,
                ["clear"] = reviewGateClear,
                ["review_task_count"] = reviewGate.ReviewTaskCount,
                ["blocked_count"] = reviewGate.BlockedCount,
                ["blockers"] = reviewGate.Blockers,
            },
            ["blockers"] = blockers,
            ["grants_execution_authority"] = false,
            ["grants_truth_write_authority"] = false,
            ["creates_task_queue"] = false,
            ["starts_run"] = false,
            ["issues_lease"] = false,
            ["usage_note"] = "This surface reports Host-mediated worker automation run eligibility; it does not approve SUGGESTED tasks, start runs, issue leases, or write task truth.",
        };
    }

    public JsonObject BuildWorkerAutomationScheduleTick(
        string? repoId = null,
        bool refreshWorkerHealth = false,
        bool dispatchRequested = false,
        bool dryRun = true)
    {
        var resolvedRepoId = ResolveRepoId(repoId);
        var readiness = BuildWorkerAutomationReadiness(resolvedRepoId, refreshWorkerHealth);
        var schedulePlan = readiness["schedule_tick_plan"]?.AsObject() ?? new JsonObject();
        var canDispatch = TryGetBoolean(schedulePlan, "schedule_tick_can_dispatch");
        var taskId = TryGetString(schedulePlan, "next_task_id");
        var executeRequested = dispatchRequested && !dryRun;
        var hostDispatchAttempted = dispatchRequested
                                    && !executeRequested
                                    && canDispatch
                                    && !string.IsNullOrWhiteSpace(taskId);
        JsonNode? delegatedResult = null;
        DelegatedExecutionResultEnvelope? delegatedEnvelope = null;
        var delegatedAccepted = false;
        JsonObject? callbackResultEvidence = null;
        var lifecycleHandoff = BuildWorkerAutomationHostLifecycleHandoff(
            resolvedRepoId,
            taskId,
            canDispatch,
            dispatchRequested,
            executeRequested,
            hostDispatchAttempted,
            delegatedEnvelope);

        if (hostDispatchAttempted)
        {
            var result = services.OperatorSurfaceService.RunDelegatedTask(
                taskId!,
                dryRun,
                ActorSessionKind.Operator,
                "scheduled-worker-automation");
            delegatedEnvelope = result;
            delegatedAccepted = result.Accepted;
            delegatedResult = JsonSerializer.SerializeToNode(result, JsonOptions);
            lifecycleHandoff = BuildWorkerAutomationHostLifecycleHandoff(
                resolvedRepoId,
                taskId,
                canDispatch,
                dispatchRequested,
                executeRequested,
                hostDispatchAttempted,
                delegatedEnvelope);
        }

        if (ShouldReadBackWorkerAutomationEvidence(taskId, dryRun, hostDispatchAttempted, delegatedEnvelope))
        {
            callbackResultEvidence = BuildWorkerDispatchPilotEvidence(taskId!);
        }

        return new JsonObject
        {
            ["kind"] = "worker_automation_schedule_tick",
            ["repo_id"] = resolvedRepoId,
            ["status"] = ResolveWorkerAutomationScheduleTickStatus(
                canDispatch,
                dispatchRequested,
                executeRequested,
                hostDispatchAttempted,
                delegatedAccepted),
            ["dispatch_requested"] = dispatchRequested,
            ["dry_run"] = dryRun,
            ["execute_requested"] = executeRequested,
            ["execute_blocked_by_schedule_tick_boundary"] = executeRequested,
            ["execute_next_command"] = string.IsNullOrWhiteSpace(taskId)
                ? "task run <task-id>"
                : $"task run {taskId}",
            ["schedule_tick_can_dispatch"] = canDispatch,
            ["host_dispatch_attempted"] = hostDispatchAttempted,
            ["host_dispatch_mode"] = executeRequested
                ? "execute_blocked"
                : hostDispatchAttempted
                ? dryRun ? "dry_run" : "execute"
                : "preflight_only",
            ["task_id"] = taskId,
            ["worker_automation_readiness_status"] = TryGetString(readiness, "status"),
            ["worker_automation_can_run_now"] = TryGetBoolean(readiness, "automation_can_run_now"),
            ["role_mode_gate"] = readiness["role_mode_gate"]?.DeepClone(),
            ["schedule_tick_plan"] = schedulePlan.DeepClone(),
            ["run_eligibility"] = readiness["run_eligibility"]?.DeepClone(),
            ["host_lifecycle_handoff"] = lifecycleHandoff,
            ["schedule_tick_receipt"] = BuildWorkerAutomationScheduleTickReceipt(
                resolvedRepoId,
                taskId,
                dispatchRequested,
                dryRun,
                canDispatch,
                hostDispatchAttempted,
                delegatedEnvelope),
            ["callback_result_check"] = BuildWorkerAutomationCallbackResultCheck(
                taskId,
                dispatchRequested,
                dryRun,
                canDispatch,
                executeRequested,
                hostDispatchAttempted,
                delegatedEnvelope,
                callbackResultEvidence),
            ["coordinator_callback"] = BuildWorkerAutomationCoordinatorCallback(taskId),
            ["callback_result_evidence"] = callbackResultEvidence,
            ["delegated_result"] = delegatedResult,
            ["delegation_actor_kind"] = ActorSessionKind.Operator.ToString(),
            ["delegation_actor_identity"] = "scheduled-worker-automation",
            ["schedule_tick_wakes_host_only"] = true,
            ["uses_existing_task_run_lifecycle"] = true,
            ["creates_second_scheduler"] = false,
            ["creates_task_queue"] = false,
            ["starts_run_without_dispatch_flag"] = false,
            ["issues_lease_without_host"] = false,
            ["grants_execution_authority"] = false,
            ["grants_truth_write_authority"] = false,
            ["usage_note"] = "This scheduled tick surface gates schedule callbacks into the existing Host task run lifecycle. Without --dispatch it is read-only preflight; with --dispatch it may prove the Host route as a dry run. Real execution is intentionally blocked here and must use task run <task-id> before evidence readback.",
        };
    }

    private JsonObject BuildWorkerAutomationScheduleTickPlan(
        string repoId,
        bool automationCanRunNow,
        RoleGovernanceRuntimePolicy roleGovernancePolicy,
        RuntimeRoleModeExecutionGateDecision roleModeGate,
        DispatchProjection dispatch,
        JsonArray readinessBlockers)
    {
        var workerSessionReadiness = ResolveScheduleBoundWorkerSessionReadiness(repoId);
        var scheduleBoundWorkers = workerSessionReadiness.LiveScheduleBoundWorkers;
        var processTrackedScheduleBoundWorkers = scheduleBoundWorkers
            .Where(IsScheduleDispatchProcessTracked)
            .ToArray();
        var supervisedScheduleBoundWorkers = scheduleBoundWorkers
            .Where(IsScheduleDispatchSupervised)
            .ToArray();
        var hostProcessHandleReadyScheduleBoundWorkers = scheduleBoundWorkers
            .Where(HasScheduleHostProcessHandleReady)
            .ToArray();
        var liveContextReceiptedWorkers = scheduleBoundWorkers
            .Where(item => !string.IsNullOrWhiteSpace(item.LastContextReceipt))
            .ToArray();
        var contextReceiptedWorkers = hostProcessHandleReadyScheduleBoundWorkers
            .Where(item => !string.IsNullOrWhiteSpace(item.LastContextReceipt))
            .ToArray();
        var selectedWorker = contextReceiptedWorkers.FirstOrDefault()
                             ?? hostProcessHandleReadyScheduleBoundWorkers.FirstOrDefault()
                             ?? liveContextReceiptedWorkers.FirstOrDefault()
                             ?? scheduleBoundWorkers.FirstOrDefault();
        var scheduleBindingReady = scheduleBoundWorkers.Length > 0;
        var processTrackingReady = processTrackedScheduleBoundWorkers.Length > 0;
        var supervisedRegistrationReady = supervisedScheduleBoundWorkers.Length > 0;
        var dispatchProofReady = hostProcessHandleReadyScheduleBoundWorkers.Length > 0;
        var contextReceiptReady = liveContextReceiptedWorkers.Length > 0;
        var tickCanDispatch = automationCanRunNow && scheduleBindingReady && dispatchProofReady && contextReceiptReady;
        var scheduleBlockers = BuildWorkerAutomationScheduleTickBlockers(
            automationCanRunNow,
            scheduleBindingReady,
            processTrackingReady,
            supervisedRegistrationReady,
            dispatchProofReady,
            contextReceiptReady,
            workerSessionReadiness,
            readinessBlockers);
        var nonLiveWorkerStopCommands = BuildScheduleBoundWorkerStopCommands(workerSessionReadiness);
        var processAliveWorkers = workerSessionReadiness.TotalScheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_alive")
            .ToArray();
        var processMissingWorkers = workerSessionReadiness.TotalScheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_missing")
            .ToArray();
        var processMismatchWorkers = workerSessionReadiness.TotalScheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_mismatch")
            .ToArray();
        var processIdentityUnverifiedWorkers = workerSessionReadiness.TotalScheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified")
            .ToArray();
        var heartbeatOnlyWorkers = workerSessionReadiness.TotalScheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only")
            .ToArray();
        var processObservedWorkers = processAliveWorkers
            .Concat(processMissingWorkers)
            .Concat(processMismatchWorkers)
            .Concat(processIdentityUnverifiedWorkers)
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();

        return new JsonObject
        {
            ["kind"] = "worker_automation_schedule_tick_plan",
            ["readiness_type"] = "scheduled_worker_callback_to_host_lifecycle_projection",
            ["readiness_claim"] = "schedule_callback_projection_only",
            ["decision"] = tickCanDispatch ? "dispatchable" : "blocked",
            ["schedule_tick_can_dispatch"] = tickCanDispatch,
            ["worker_automation_can_run_now"] = automationCanRunNow,
            ["role_mode_gate"] = BuildRoleModeGateSurface(roleGovernancePolicy, roleModeGate),
            ["schedule_bound_worker_ready"] = scheduleBindingReady,
            ["schedule_bound_worker_process_tracking_ready"] = processTrackingReady,
            ["schedule_bound_worker_supervised_registration_ready"] = supervisedRegistrationReady,
            ["schedule_bound_worker_dispatch_proof_ready"] = dispatchProofReady,
            ["context_receipt_ready"] = contextReceiptReady,
            ["schedule_bound_worker_session_count"] = workerSessionReadiness.TotalScheduleBoundWorkers.Length,
            ["live_schedule_bound_worker_session_count"] = scheduleBoundWorkers.Length,
            ["dispatch_eligible_schedule_bound_worker_session_count"] = hostProcessHandleReadyScheduleBoundWorkers.Length,
            ["live_context_receipted_worker_session_count"] = liveContextReceiptedWorkers.Length,
            ["context_receipted_worker_session_count"] = contextReceiptedWorkers.Length,
            ["worker_session_liveness_status"] = ResolveScheduleBoundWorkerLivenessStatus(workerSessionReadiness),
            ["worker_session_process_tracking_status"] = ResolveScheduleBoundWorkerProcessTrackingStatus(workerSessionReadiness),
            ["worker_session_dispatch_proof_status"] = ResolveScheduleBoundWorkerDispatchProofStatus(workerSessionReadiness),
            ["schedule_dispatch_process_ownership_policy"] = "dispatch_requires_host_owned_process_handle; pid_start_time_is_identity_evidence_only",
            ["schedule_dispatch_uses_process_identity_proof"] = false,
            ["schedule_dispatch_process_identity_evidence_ready"] = processTrackingReady,
            ["schedule_dispatch_uses_host_owned_process_handle"] = dispatchProofReady,
            ["host_process_handle_ready_schedule_bound_worker_session_count"] = hostProcessHandleReadyScheduleBoundWorkers.Length,
            ["immediate_death_detection_ready_schedule_bound_worker_session_count"] = hostProcessHandleReadyScheduleBoundWorkers.Length,
            ["supervised_schedule_bound_worker_session_count"] = supervisedScheduleBoundWorkers.Length,
            ["supervised_schedule_bound_worker_session_ids"] = ToJsonArray(supervisedScheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["process_observed_schedule_bound_worker_session_count"] = processObservedWorkers.Length,
            ["process_observed_schedule_bound_worker_session_ids"] = ToJsonArray(processObservedWorkers.Select(item => item.ActorSessionId)),
            ["process_alive_schedule_bound_worker_session_count"] = processAliveWorkers.Length,
            ["process_alive_schedule_bound_worker_session_ids"] = ToJsonArray(processAliveWorkers.Select(item => item.ActorSessionId)),
            ["process_missing_schedule_bound_worker_session_count"] = processMissingWorkers.Length,
            ["process_mismatch_schedule_bound_worker_session_count"] = processMismatchWorkers.Length,
            ["process_identity_unverified_schedule_bound_worker_session_count"] = processIdentityUnverifiedWorkers.Length,
            ["heartbeat_only_schedule_bound_worker_session_count"] = heartbeatOnlyWorkers.Length,
            ["process_tracked_schedule_bound_worker_session_count"] = processObservedWorkers.Length,
            ["process_tracked_schedule_bound_worker_session_count_deprecated"] = true,
            ["process_tracked_schedule_bound_worker_session_count_replaced_by"] = "process_observed_schedule_bound_worker_session_count",
            ["process_tracked_schedule_bound_worker_session_count_semantics"] = "deprecated_alias_for_process_observed_metadata_not_dispatch_eligibility",
            ["worker_session_freshness_window_seconds"] = (int)ActorSessionLivenessRules.FreshnessWindow.TotalSeconds,
            ["worker_session_liveness_checked_at"] = workerSessionReadiness.CheckedAt,
            ["stale_schedule_bound_worker_session_count"] = workerSessionReadiness.StaleScheduleBoundWorkers.Length,
            ["closed_schedule_bound_worker_session_count"] = workerSessionReadiness.ClosedScheduleBoundWorkers.Length,
            ["stale_schedule_bound_worker_session_ids"] = ToJsonArray(workerSessionReadiness.StaleScheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["closed_schedule_bound_worker_session_ids"] = ToJsonArray(workerSessionReadiness.ClosedScheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["process_missing_schedule_bound_worker_session_ids"] = ToJsonArray(processMissingWorkers.Select(item => item.ActorSessionId)),
            ["process_mismatch_schedule_bound_worker_session_ids"] = ToJsonArray(processMismatchWorkers.Select(item => item.ActorSessionId)),
            ["process_identity_unverified_schedule_bound_worker_session_ids"] = ToJsonArray(processIdentityUnverifiedWorkers.Select(item => item.ActorSessionId)),
            ["heartbeat_only_schedule_bound_worker_session_ids"] = ToJsonArray(heartbeatOnlyWorkers.Select(item => item.ActorSessionId)),
            ["non_live_worker_stop_commands"] = ToJsonArray(nonLiveWorkerStopCommands),
            ["selected_worker_actor_session_id"] = selectedWorker?.ActorSessionId,
            ["selected_worker_identity"] = selectedWorker?.ActorIdentity,
            ["selected_worker_schedule_binding"] = selectedWorker?.ScheduleBinding,
            ["selected_worker_context_receipt"] = selectedWorker?.LastContextReceipt,
            ["selected_worker_health_posture"] = selectedWorker?.HealthPosture,
            ["selected_worker_process_id"] = selectedWorker?.ProcessId,
            ["selected_worker_process_started_at"] = selectedWorker?.ProcessStartedAt,
            ["selected_worker_registration_mode"] = selectedWorker?.RegistrationMode.ToString().ToLowerInvariant(),
            ["selected_worker_worker_instance_id"] = selectedWorker?.WorkerInstanceId,
            ["selected_worker_supervisor_launch_token_id"] = selectedWorker?.SupervisorLaunchTokenId,
            ["selected_worker_supervisor_capability_mode"] = ResolveScheduleWorkerSupervisorCapabilityMode(selectedWorker),
            ["selected_worker_process_ownership_status"] = ResolveScheduleWorkerProcessOwnershipStatus(selectedWorker),
            ["selected_worker_process_proof_source"] = ResolveScheduleWorkerProcessProofSource(selectedWorker),
            ["selected_worker_host_process_handle_ready"] = selectedWorker is not null && HasScheduleHostProcessHandleReady(selectedWorker),
            ["selected_worker_immediate_death_detection_ready"] = selectedWorker is not null && HasScheduleHostProcessHandleReady(selectedWorker),
            ["selected_worker_process_tracking_status"] = selectedWorker is null
                ? null
                : ActorSessionLivenessRules.ResolveProcessTrackingStatus(selectedWorker),
            ["selected_worker_dispatch_proof_status"] = selectedWorker is null
                ? null
                : ResolveScheduleDispatchProofStatus(selectedWorker),
            ["next_task_id"] = dispatch.NextTaskId,
            ["host_lifecycle_command"] = tickCanDispatch
                ? ResolveWorkerAutomationNextCommand(true, readinessBlockers, dispatch)
                : "inspect worker-automation-readiness",
            ["host_lifecycle_handoff"] = BuildWorkerAutomationHostLifecycleHandoff(
                repoId,
                dispatch.NextTaskId,
                tickCanDispatch,
                dispatchRequested: false,
                executeRequested: false,
                hostDispatchAttempted: false,
                delegatedResult: null),
            ["callback_result_check_command"] = string.IsNullOrWhiteSpace(dispatch.NextTaskId)
                ? "api worker-dispatch-pilot-evidence <task-id>"
                : $"api worker-dispatch-pilot-evidence {dispatch.NextTaskId}",
            ["coordinator_callback_required"] = true,
            ["result_ingestion_semantics"] = string.IsNullOrWhiteSpace(dispatch.NextTaskId)
                ? "task ingest-result <task-id> semantics after a governed worker result is available"
                : $"task ingest-result {dispatch.NextTaskId} semantics after a governed worker result is available",
            ["callback_evidence_required"] = true,
            ["required_evidence"] = new JsonArray
            {
                "schedule_tick_receipt",
                "worker_execution_result_envelope",
                "completion_claim",
                "review_bundle",
            },
            ["planner_reentry_required_after_review"] = true,
            ["schedule_tick_wakes_host_only"] = true,
            ["creates_second_scheduler"] = false,
            ["creates_task_queue"] = false,
            ["starts_run"] = false,
            ["issues_lease"] = false,
            ["grants_execution_authority"] = false,
            ["grants_truth_write_authority"] = false,
            ["readiness_blocker_count"] = readinessBlockers.Count,
            ["schedule_blocker_count"] = scheduleBlockers.Count,
            ["schedule_blockers"] = scheduleBlockers,
            ["usage_note"] = "This plan only projects whether a registered schedule callback can wake Host to use the existing task run lifecycle; it does not run the task, issue a lease, approve review, or write truth.",
        };
    }

    private static ProviderHealthRecord? ResolveSelectedHealth(
        IReadOnlyList<ProviderHealthRecord> health,
        string? selectedBackendId)
    {
        if (string.IsNullOrWhiteSpace(selectedBackendId))
        {
            return null;
        }

        return health.FirstOrDefault(item => string.Equals(item.BackendId, selectedBackendId, StringComparison.Ordinal));
    }

    private ScheduleBoundWorkerSessionReadiness ResolveScheduleBoundWorkerSessionReadiness(string repoId)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var scheduleBoundWorkers = services.OperatorApiService.GetActorSessions()
            .Where(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .Where(item => item.Kind == ActorSessionKind.Worker)
            .Where(item => item.State != ActorSessionState.Stopped)
            .Where(item => !string.IsNullOrWhiteSpace(item.ScheduleBinding))
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var liveness = ActorSessionLivenessRules.Classify(scheduleBoundWorkers, checkedAt);

        return new ScheduleBoundWorkerSessionReadiness(
            checkedAt,
            scheduleBoundWorkers,
            liveness.LiveSessions.ToArray(),
            liveness.StaleSessions.ToArray(),
            liveness.ClosedSessions.ToArray());
    }

    private static string ResolveScheduleBoundWorkerLivenessStatus(ScheduleBoundWorkerSessionReadiness readiness)
    {
        if (readiness.LiveScheduleBoundWorkers.Length > 0)
        {
            return "ready";
        }

        if (readiness.TotalScheduleBoundWorkers.Length == 0)
        {
            return "not_registered";
        }

        if (readiness.ClosedScheduleBoundWorkers.Length > 0)
        {
            return "closed_or_unavailable";
        }

        if (readiness.StaleScheduleBoundWorkers.Length > 0)
        {
            return "stale";
        }

        return "blocked";
    }

    private static string ResolveScheduleBoundWorkerProcessTrackingStatus(ScheduleBoundWorkerSessionReadiness readiness)
    {
        if (readiness.TotalScheduleBoundWorkers.Length == 0)
        {
            return "not_registered";
        }

        var statuses = readiness.TotalScheduleBoundWorkers
            .Select(ActorSessionLivenessRules.ResolveProcessTrackingStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (statuses.Length > 1)
        {
            return "mixed";
        }

        return statuses[0] switch
        {
            "process_alive" => "process_tracked",
            "process_missing" => "process_missing",
            "process_mismatch" => "process_mismatch",
            "process_identity_unverified" => "process_identity_unverified",
            "heartbeat_only" => "heartbeat_only",
            _ => "unknown",
        };
    }

    private string ResolveScheduleBoundWorkerDispatchProofStatus(ScheduleBoundWorkerSessionReadiness readiness)
    {
        if (readiness.TotalScheduleBoundWorkers.Length == 0)
        {
            return "not_registered";
        }

        var statuses = readiness.TotalScheduleBoundWorkers
            .Select(ResolveScheduleDispatchProofStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (statuses.Length > 1)
        {
            return "mixed";
        }

        return statuses[0];
    }

    private static bool IsScheduleDispatchProcessTracked(ActorSessionRecord session)
    {
        return ActorSessionLivenessRules.ResolveProcessTrackingStatus(session) == "process_alive";
    }

    private static bool IsScheduleDispatchSupervised(ActorSessionRecord session)
    {
        return session.RegistrationMode == ActorSessionRegistrationMode.Supervised
               && !string.IsNullOrWhiteSpace(session.WorkerInstanceId)
               && !string.IsNullOrWhiteSpace(session.SupervisorLaunchTokenId);
    }

    private bool HasScheduleDispatchProof(ActorSessionRecord session)
    {
        return HasScheduleHostProcessHandleReady(session);
    }

    private bool HasScheduleHostProcessHandleReady(ActorSessionRecord session)
    {
        return HasSupervisorHostProcessHandleProof(ResolveBoundWorkerSupervisor(session), session);
    }

    private string ResolveScheduleDispatchProofStatus(ActorSessionRecord session)
    {
        var processTracked = IsScheduleDispatchProcessTracked(session);
        var supervised = IsScheduleDispatchSupervised(session);
        return (processTracked, supervised) switch
        {
            _ when HasScheduleHostProcessHandleReady(session) => "host_process_handle_ready",
            (true, true) => "supervised_process_alive_without_host_handle",
            (true, false) => "process_alive_without_host_handle",
            (false, true) => "supervised_registration_without_live_process",
            _ => "missing",
        };
    }

    private string? ResolveScheduleWorkerSupervisorCapabilityMode(ActorSessionRecord? session)
    {
        if (session is null)
        {
            return null;
        }

        if (HasScheduleHostProcessHandleReady(session))
        {
            return "host_owned_process_handle";
        }

        return IsScheduleDispatchSupervised(session)
            ? "registration_only_launch_token"
            : "manual_actor_session";
    }

    private string? ResolveScheduleWorkerProcessOwnershipStatus(ActorSessionRecord? session)
    {
        if (session is null)
        {
            return null;
        }

        var processStatus = ActorSessionLivenessRules.ResolveProcessTrackingStatus(session);
        if (HasScheduleHostProcessHandleReady(session))
        {
            return "host_owned_process_handle_ready";
        }

        if (IsScheduleDispatchSupervised(session))
        {
            return processStatus == "process_alive"
                ? "supervised_actor_session_process_identity_proof_no_host_handle"
                : $"supervised_actor_session_{processStatus}_no_host_handle";
        }

        return processStatus == "process_alive"
            ? "manual_actor_session_process_identity_proof_no_host_handle"
            : $"manual_actor_session_{processStatus}_no_host_handle";
    }

    private static string? ResolveScheduleWorkerProcessProofSource(ActorSessionRecord? session)
    {
        if (session is null)
        {
            return null;
        }

        return session.ProcessId.HasValue && session.ProcessStartedAt.HasValue
            ? "actor_session_process_id_and_start_time"
            : "none";
    }

    private WorkerSupervisorInstanceRecord? ResolveBoundWorkerSupervisor(ActorSessionRecord session)
    {
        var supervisorRecords = services.ActorSessionService.ListWorkerSupervisorInstances(session.RepoId);
        if (!string.IsNullOrWhiteSpace(session.WorkerInstanceId))
        {
            var byWorkerInstance = supervisorRecords.FirstOrDefault(item =>
                string.Equals(item.WorkerInstanceId, session.WorkerInstanceId, StringComparison.Ordinal));
            if (byWorkerInstance is not null)
            {
                return byWorkerInstance;
            }
        }

        return supervisorRecords.FirstOrDefault(item =>
            string.Equals(item.ActorSessionId, session.ActorSessionId, StringComparison.Ordinal));
    }

    private static bool HasSupervisorHostProcessHandleProof(
        WorkerSupervisorInstanceRecord? record,
        ActorSessionRecord session)
    {
        if (record is null
            || record.OwnershipMode is not WorkerSupervisorOwnershipMode.HostOwned
            || record.State is not WorkerSupervisorInstanceState.Running
            || string.IsNullOrWhiteSpace(record.HostProcessHandleId)
            || string.IsNullOrWhiteSpace(record.HostProcessHandleOwnerSessionId)
            || record.HostProcessHandleAcquiredAt is null)
        {
            return false;
        }

        if (!string.Equals(record.ActorSessionId, session.ActorSessionId, StringComparison.Ordinal)
            || !string.Equals(record.WorkerInstanceId, session.WorkerInstanceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(record.HostSessionId)
            && !string.Equals(record.HostSessionId, record.HostProcessHandleOwnerSessionId, StringComparison.Ordinal))
        {
            return false;
        }

        return record.ProcessId == session.ProcessId
               && record.ProcessStartedAt == session.ProcessStartedAt;
    }

    private static WorkerBackendDescriptor? ResolveSelectedBackend(
        IReadOnlyList<WorkerBackendDescriptor> backends,
        string? selectedBackendId)
    {
        if (string.IsNullOrWhiteSpace(selectedBackendId))
        {
            return null;
        }

        return backends.FirstOrDefault(item => string.Equals(item.BackendId, selectedBackendId, StringComparison.Ordinal));
    }

    private WorkerSelectionDecision ResolveWorkerAutomationSelection(
        string repoId,
        string? taskId,
        out string? selectionError)
    {
        try
        {
            selectionError = null;
            return services.OperatorApiService.GetWorkerSelection(repoId, taskId);
        }
        catch (Exception exception)
        {
            selectionError = exception.Message;
            return new WorkerSelectionDecision
            {
                RepoId = repoId,
                TaskId = taskId,
                Allowed = false,
                ReasonCode = "worker_selection_unavailable",
                Summary = $"Worker selection could not be evaluated: {exception.Message}",
            };
        }
    }

    private static string ResolveWorkerRuntimeStatus(
        WorkerSelectionDecision selection,
        ProviderHealthRecord? selectedHealth,
        WorkerSelectionCandidate? selectedCandidate,
        WorkerBackendDescriptor? selectedBackend)
    {
        if (!selection.Allowed || string.IsNullOrWhiteSpace(selection.SelectedBackendId))
        {
            return "selection_blocked";
        }

        if (!IsExternalAppCliWorkerBackend(selectedBackend))
        {
            return "external_app_cli_required";
        }

        if (selectedHealth is not null)
        {
            return selectedHealth.State switch
            {
                WorkerBackendHealthState.Healthy => "available",
                WorkerBackendHealthState.Degraded => "degraded",
                WorkerBackendHealthState.Unknown => "health_unknown",
                WorkerBackendHealthState.Unavailable => "unavailable",
                WorkerBackendHealthState.Disabled => "disabled",
                _ => "health_unknown",
            };
        }

        return string.Equals(selectedCandidate?.HealthState, "healthy", StringComparison.OrdinalIgnoreCase)
            ? "available"
            : "health_not_recorded";
    }

    private static bool IsExternalAppCliWorkerBackend(WorkerBackendDescriptor? backend)
    {
        if (backend is null || string.Equals(backend.ProviderId, "null", StringComparison.Ordinal))
        {
            return false;
        }

        if (!backend.Capabilities.SupportsExecution)
        {
            return false;
        }

        var protocolFamily = backend.ProtocolFamily.ToLowerInvariant();
        var requestFamily = backend.RequestFamily.ToLowerInvariant();
        return protocolFamily.Contains("cli", StringComparison.Ordinal)
               || protocolFamily.Contains("app", StringComparison.Ordinal)
               || requestFamily.Contains("cli", StringComparison.Ordinal)
               || requestFamily.Contains("exec", StringComparison.Ordinal);
    }

    private WorkerAutomationReviewGateReadiness BuildReviewGateAutomationReadiness(IEnumerable<Carves.Runtime.Domain.Tasks.TaskNode> tasks)
    {
        var reviewTasks = tasks
            .Where(task => task.Status is DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait)
            .OrderBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
        var projectionService = new ReviewEvidenceProjectionService(services.Paths.RepoRoot, services.GitClient);
        var blockers = new JsonArray();

        foreach (var task in reviewTasks.Take(10))
        {
            var reviewArtifact = services.ArtifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
            var workerArtifact = services.ArtifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
            var projection = projectionService.Build(task, reviewArtifact, workerArtifact);
            if (projection.CanFinalApprove)
            {
                continue;
            }

            blockers.Add(new JsonObject
            {
                ["task_id"] = task.TaskId,
                ["status"] = task.Status.ToString(),
                ["review_evidence_status"] = projection.Status,
                ["summary"] = projection.Summary,
                ["closure_decision"] = projection.ClosureDecision,
                ["closure_writeback_allowed"] = projection.ClosureWritebackAllowed,
                ["missing_before_writeback_count"] = projection.MissingBeforeWriteback.Count,
                ["missing_after_writeback_count"] = projection.MissingAfterWriteback.Count,
                ["follow_up_actions"] = ToJsonArray(projection.FollowUpActions),
            });
        }

        return new WorkerAutomationReviewGateReadiness(reviewTasks.Length, blockers.Count, blockers);
    }

    private static JsonObject BuildWorkerAutomationRunEligibility(
        bool eligible,
        RuntimeRoleModeExecutionGateDecision roleModeGate,
        bool hostRuntimeReady,
        bool dispatchable,
        bool workerRuntimeAvailable,
        bool reviewGateClear,
        string hostReadiness,
        DispatchProjection dispatch,
        WorkerSelectionDecision selection,
        WorkerBackendDescriptor? selectedBackend,
        bool selectedBackendExternalAppCli,
        string? selectionError,
        string workerRuntimeStatus,
        WorkerAutomationReviewGateReadiness reviewGate,
        JsonArray blockers)
    {
        return new JsonObject
        {
            ["kind"] = "worker_automation_run_eligibility",
            ["eligible"] = eligible,
            ["decision"] = eligible ? "eligible" : "blocked",
            ["decision_basis"] = "role_mode_and_host_health_and_dispatchable_task_and_worker_runtime_and_review_gate",
            ["role_mode_allowed"] = roleModeGate.Allowed,
            ["role_mode_outcome"] = roleModeGate.Outcome,
            ["role_mode_summary"] = roleModeGate.Summary,
            ["host_ready"] = hostRuntimeReady,
            ["host_readiness"] = hostReadiness,
            ["dispatchable_task_ready"] = dispatchable,
            ["worker_runtime_ready"] = workerRuntimeAvailable,
            ["review_gate_clear"] = reviewGateClear,
            ["next_task_id"] = dispatch.NextTaskId,
            ["selected_backend_id"] = selection.SelectedBackendId,
            ["selected_protocol_family"] = selectedBackend?.ProtocolFamily,
            ["selected_request_family"] = selectedBackend?.RequestFamily,
            ["selected_backend_external_app_cli"] = selectedBackendExternalAppCli,
            ["selection_error"] = selectionError,
            ["worker_runtime_status"] = workerRuntimeStatus,
            ["review_blocked_count"] = reviewGate.BlockedCount,
            ["blocker_count"] = blockers.Count,
            ["blockers"] = blockers.DeepClone(),
            ["next_action"] = ResolveWorkerAutomationNextAction(eligible, blockers, dispatch),
            ["next_command"] = ResolveWorkerAutomationNextCommand(eligible, blockers, dispatch),
            ["run_not_started"] = true,
            ["lease_not_issued"] = true,
            ["truth_writeback_not_allowed"] = true,
        };
    }

    private static JsonArray BuildWorkerAutomationBlockers(
        RuntimeRoleModeExecutionGateDecision roleModeGate,
        bool hostRuntimeReady,
        bool dispatchable,
        bool workerRuntimeAvailable,
        bool reviewGateClear,
        string hostReadiness,
        DispatchProjection dispatch,
        WorkerSelectionDecision selection,
        WorkerBackendDescriptor? selectedBackend,
        bool selectedBackendExternalAppCli,
        string? selectionError,
        string workerRuntimeStatus,
        WorkerAutomationReviewGateReadiness reviewGate)
    {
        var blockers = new JsonArray();
        if (!roleModeGate.Allowed)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "role_mode",
                ["code"] = roleModeGate.Outcome,
                ["blocking"] = true,
                ["summary"] = roleModeGate.Summary,
                ["next_action"] = roleModeGate.NextAction,
                ["next_command"] = "policy inspect",
                ["main_thread_direct_mode"] = true,
                ["role_automation_frozen"] = true,
            });
        }

        if (!hostRuntimeReady)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "host_health",
                ["code"] = "host_control_plane_not_ready",
                ["blocking"] = true,
                ["readiness"] = hostReadiness,
                ["summary"] = "Host control-plane readiness did not pass.",
                ["next_action"] = "Start or reconcile the resident Host before worker automation dispatch.",
                ["next_command"] = "carves host ensure --json",
            });
        }

        if (!dispatchable)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "dispatch",
                ["code"] = "dispatch_not_available",
                ["blocking"] = true,
                ["summary"] = dispatch.Summary,
                ["idle_reason"] = dispatch.IdleReason,
                ["next_action"] = dispatch.RecommendedNextAction,
                ["next_command"] = dispatch.RecommendedNextCommand,
            });
        }

        if (!workerRuntimeAvailable)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "worker_runtime",
                ["code"] = "worker_runtime_not_available",
                ["blocking"] = true,
                ["status"] = workerRuntimeStatus,
                ["selection_allowed"] = selection.Allowed,
                ["selected_backend_id"] = selection.SelectedBackendId,
                ["selected_protocol_family"] = selectedBackend?.ProtocolFamily,
                ["selected_request_family"] = selectedBackend?.RequestFamily,
                ["selected_backend_external_app_cli"] = selectedBackendExternalAppCli,
                ["sdk_api_worker_boundary"] = "closed_until_separate_governed_activation",
                ["selection_error"] = selectionError,
                ["reason_code"] = selection.ReasonCode,
                ["summary"] = selection.Summary,
            });
        }

        if (!reviewGateClear)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "review_gate",
                ["code"] = "review_gate_blocked",
                ["blocking"] = true,
                ["review_task_count"] = reviewGate.ReviewTaskCount,
                ["blocked_count"] = reviewGate.BlockedCount,
                ["summary"] = "One or more review/approval-wait tasks cannot be final-approved from current evidence.",
            });
        }

        return blockers;
    }

    private static JsonObject BuildRoleModeGateSurface(
        RoleGovernanceRuntimePolicy policy,
        RuntimeRoleModeExecutionGateDecision decision)
    {
        return new JsonObject
        {
            ["checked"] = true,
            ["allowed"] = decision.Allowed,
            ["outcome"] = decision.Outcome,
            ["summary"] = decision.Summary,
            ["next_action"] = decision.NextAction,
            ["role_mode"] = policy.RoleMode,
            ["planner_worker_split_enabled"] = policy.PlannerWorkerSplitEnabled,
            ["worker_delegation_enabled"] = policy.WorkerDelegationEnabled,
            ["scheduler_auto_dispatch_enabled"] = policy.SchedulerAutoDispatchEnabled,
            ["main_thread_direct_mode"] = !decision.Allowed,
        };
    }

    private JsonArray BuildWorkerAutomationScheduleTickBlockers(
        bool automationCanRunNow,
        bool scheduleBindingReady,
        bool processTrackingReady,
        bool supervisedRegistrationReady,
        bool dispatchProofReady,
        bool contextReceiptReady,
        ScheduleBoundWorkerSessionReadiness workerSessionReadiness,
        JsonArray readinessBlockers)
    {
        var blockers = new JsonArray();
        if (!scheduleBindingReady)
        {
            var nonLiveWorkerStopCommands = BuildScheduleBoundWorkerStopCommands(workerSessionReadiness);
            var noLiveWorkerCode = workerSessionReadiness.TotalScheduleBoundWorkers.Length > 0
                ? ResolveScheduleBoundWorkerLivenessStatus(workerSessionReadiness) switch
                {
                    "closed_or_unavailable" => "schedule_bound_worker_session_closed",
                    "stale" => "schedule_bound_worker_session_stale",
                    _ => "no_live_schedule_bound_worker_session",
                }
                : "no_schedule_bound_worker_session";
            blockers.Add(new JsonObject
            {
                ["family"] = "worker_callback_binding",
                ["code"] = noLiveWorkerCode,
                ["blocking"] = true,
                ["summary"] = workerSessionReadiness.TotalScheduleBoundWorkers.Length == 0
                    ? "No active worker actor session has a schedule binding for this repo."
                    : "Schedule-bound worker actor sessions exist, but none are currently live enough for scheduled dispatch.",
                ["liveness_status"] = ResolveScheduleBoundWorkerLivenessStatus(workerSessionReadiness),
                ["process_tracking_status"] = ResolveScheduleBoundWorkerProcessTrackingStatus(workerSessionReadiness),
                ["freshness_window_seconds"] = (int)ActorSessionLivenessRules.FreshnessWindow.TotalSeconds,
                ["stale_session_ids"] = ToJsonArray(workerSessionReadiness.StaleScheduleBoundWorkers.Select(item => item.ActorSessionId)),
                ["closed_session_ids"] = ToJsonArray(workerSessionReadiness.ClosedScheduleBoundWorkers.Select(item => item.ActorSessionId)),
                ["process_missing_session_ids"] = ToJsonArray(workerSessionReadiness.TotalScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_missing")
                    .Select(item => item.ActorSessionId)),
                ["process_mismatch_session_ids"] = ToJsonArray(workerSessionReadiness.TotalScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_mismatch")
                    .Select(item => item.ActorSessionId)),
                ["process_identity_unverified_session_ids"] = ToJsonArray(workerSessionReadiness.TotalScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified")
                    .Select(item => item.ActorSessionId)),
                ["heartbeat_only_session_ids"] = ToJsonArray(workerSessionReadiness.TotalScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only")
                    .Select(item => item.ActorSessionId)),
                ["non_live_worker_liveness_reasons"] = ToJsonArray(workerSessionReadiness.ClosedScheduleBoundWorkers
                    .Concat(workerSessionReadiness.StaleScheduleBoundWorkers)
                    .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
                    .Select(item => $"{item.ActorSessionId}:{ActorSessionLivenessRules.ResolveNonLiveReason(item, workerSessionReadiness.CheckedAt)}")),
                ["non_live_worker_stop_commands"] = ToJsonArray(nonLiveWorkerStopCommands),
                ["next_action"] = workerSessionReadiness.TotalScheduleBoundWorkers.Length == 0
                    ? "Register or refresh a worker actor session with --schedule-binding and --context-receipt."
                    : "Refresh the worker actor session from a live thread, or stop the stale/closed actor session before scheduled dispatch.",
                ["next_command"] = workerSessionReadiness.TotalScheduleBoundWorkers.Length == 0
                    ? "api actor-session-register --kind worker --identity <id> --schedule-binding <id> --context-receipt <id>"
                    : nonLiveWorkerStopCommands.FirstOrDefault()
                      ?? "api actor-session-register --kind worker --identity <id> --schedule-binding <id> --context-receipt <id> --health healthy",
            });
        }

        if (scheduleBindingReady && !dispatchProofReady)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "worker_callback_binding",
                ["code"] = "schedule_bound_worker_host_process_handle_required",
                ["dispatch_proof_code"] = "host_process_handle_required_for_schedule_dispatch",
                ["blocking"] = true,
                ["summary"] = "Schedule-bound worker sessions are present, but scheduled dispatch now requires a Host-owned process handle. PID/start-time process proof is retained only as identity evidence until the supervisor owns the worker process.",
                ["liveness_status"] = ResolveScheduleBoundWorkerLivenessStatus(workerSessionReadiness),
                ["process_tracking_status"] = ResolveScheduleBoundWorkerProcessTrackingStatus(workerSessionReadiness),
                ["dispatch_proof_status"] = ResolveScheduleBoundWorkerDispatchProofStatus(workerSessionReadiness),
                ["process_tracking_ready"] = processTrackingReady,
                ["supervised_registration_ready"] = supervisedRegistrationReady,
                ["dispatch_proof_ready"] = dispatchProofReady,
                ["host_process_handle_ready"] = false,
                ["heartbeat_only_session_ids"] = ToJsonArray(workerSessionReadiness.LiveScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only")
                    .Select(item => item.ActorSessionId)),
                ["process_identity_unverified_session_ids"] = ToJsonArray(workerSessionReadiness.LiveScheduleBoundWorkers
                    .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified")
                    .Select(item => item.ActorSessionId)),
                ["next_action"] = "Use manual Host task run, or wait for Host-owned worker supervisor launch to provide a live process handle before enabling scheduled dispatch.",
                ["next_command"] = "api worker-supervisor-launch --repo-id <repo-id> --identity <id> --worker-instance-id <id>",
            });
        }

        if (scheduleBindingReady && processTrackingReady && !contextReceiptReady)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "worker_callback_binding",
                ["code"] = "missing_worker_context_receipt",
                ["blocking"] = true,
                ["summary"] = "A schedule-bound worker session exists, but no worker session has a context receipt.",
                ["next_action"] = "Refresh the worker actor session with the latest context receipt before scheduled dispatch.",
                ["next_command"] = "api actor-session-register --kind worker --identity <id> --schedule-binding <id> --context-receipt <id>",
            });
        }

        if (!automationCanRunNow)
        {
            blockers.Add(new JsonObject
            {
                ["family"] = "worker_automation_readiness",
                ["code"] = "worker_automation_runtime_blocked",
                ["blocking"] = true,
                ["summary"] = "Worker automation runtime readiness is blocked; schedule callback cannot dispatch until Host, dispatch, worker runtime, and review gate are clear.",
                ["readiness_blocker_count"] = readinessBlockers.Count,
                ["readiness_blockers"] = readinessBlockers.DeepClone(),
            });
        }

        return blockers;
    }

    private static string[] BuildScheduleBoundWorkerStopCommands(ScheduleBoundWorkerSessionReadiness readiness)
    {
        return ActorSessionLivenessRules.BuildStopCommands(
            readiness.ClosedScheduleBoundWorkers.Concat(readiness.StaleScheduleBoundWorkers),
            "worker-thread-not-live");
    }

    private static string ResolveWorkerAutomationNextAction(bool eligible, JsonArray blockers, DispatchProjection dispatch)
    {
        if (eligible)
        {
            return "Host may dispatch the next worker run through the governed task run lifecycle.";
        }

        var firstBlocker = blockers.OfType<JsonObject>().FirstOrDefault();
        if (firstBlocker is not null
            && firstBlocker.TryGetPropertyValue("next_action", out var nextAction)
            && !string.IsNullOrWhiteSpace(nextAction?.GetValue<string>()))
        {
            return nextAction.GetValue<string>();
        }

        return string.IsNullOrWhiteSpace(dispatch.RecommendedNextAction)
            ? "Resolve the reported worker automation blockers before dispatch."
            : dispatch.RecommendedNextAction;
    }

    private static string ResolveWorkerAutomationNextCommand(bool eligible, JsonArray blockers, DispatchProjection dispatch)
    {
        if (eligible)
        {
            return string.IsNullOrWhiteSpace(dispatch.NextTaskId)
                ? "task run <task-id>"
                : $"task run {dispatch.NextTaskId}";
        }

        var firstBlocker = blockers.OfType<JsonObject>().FirstOrDefault();
        if (firstBlocker is not null
            && firstBlocker.TryGetPropertyValue("next_command", out var nextCommand)
            && !string.IsNullOrWhiteSpace(nextCommand?.GetValue<string>()))
        {
            return nextCommand.GetValue<string>();
        }

        return string.IsNullOrWhiteSpace(dispatch.RecommendedNextCommand)
            ? "inspect worker-automation-readiness"
            : dispatch.RecommendedNextCommand;
    }

    private static string ResolveWorkerAutomationScheduleTickStatus(
        bool canDispatch,
        bool dispatchRequested,
        bool executeRequested,
        bool hostDispatchAttempted,
        bool delegatedAccepted)
    {
        if (!canDispatch)
        {
            return "blocked";
        }

        if (!dispatchRequested)
        {
            return "ready_preflight_only";
        }

        if (executeRequested)
        {
            return "execute_blocked_use_task_run";
        }

        if (!hostDispatchAttempted)
        {
            return "blocked";
        }

        return delegatedAccepted
            ? "delegated_to_host"
            : "host_delegation_rejected";
    }

    private static JsonObject BuildWorkerAutomationHostLifecycleHandoff(
        string repoId,
        string? taskId,
        bool canDispatch,
        bool dispatchRequested,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult)
    {
        var normalizedTaskId = string.IsNullOrWhiteSpace(taskId) ? "<task-id>" : taskId;
        var taskRunCommand = $"task run {normalizedTaskId}";
        var evidenceCommand = $"api worker-dispatch-pilot-evidence {normalizedTaskId}";
        var status = ResolveWorkerAutomationHostLifecycleHandoffStatus(
            canDispatch,
            dispatchRequested,
            executeRequested,
            hostDispatchAttempted,
            delegatedResult);

        return new JsonObject
        {
            ["kind"] = "worker_automation_host_lifecycle_handoff",
            ["schema_version"] = "worker-automation-host-lifecycle-handoff.v1",
            ["handoff_id"] = $"worker-automation-handoff:{repoId}:{normalizedTaskId}",
            ["repo_id"] = repoId,
            ["task_id"] = taskId,
            ["status"] = status,
            ["ready_for_task_run"] = canDispatch,
            ["dispatch_requested"] = dispatchRequested,
            ["execute_requested"] = executeRequested,
            ["schedule_tick_dispatch_attempted"] = hostDispatchAttempted,
            ["schedule_tick_dispatch_accepted"] = delegatedResult?.Accepted ?? false,
            ["task_run_command"] = taskRunCommand,
            ["post_run_readback_command"] = evidenceCommand,
            ["expected_execution_run_id_source"] = "task run result.execution_run_id",
            ["expected_worker_run_id_source"] = "task run result.run_id",
            ["readback_execution_run_id_source"] = "worker-dispatch-pilot-evidence.dispatch.latest_run_id",
            ["readback_result_execution_run_id_source"] = "worker-dispatch-pilot-evidence.result_ingestion.result_execution_run_id",
            ["run_id_required_after_task_run"] = true,
            ["result_ingestion_required_after_task_run"] = true,
            ["review_bundle_required_after_task_run"] = true,
            ["callback_reports_handoff_only"] = true,
            ["schedule_tick_executes_task"] = false,
            ["schedule_tick_grants_execution_authority"] = false,
            ["writes_task_truth"] = false,
            ["creates_task_queue"] = false,
            ["creates_execution_truth_root"] = false,
            ["next_action"] = ResolveWorkerAutomationHostLifecycleHandoffNextAction(
                canDispatch,
                executeRequested,
                hostDispatchAttempted,
                delegatedResult,
                taskRunCommand,
                evidenceCommand),
        };
    }

    private static string ResolveWorkerAutomationHostLifecycleHandoffStatus(
        bool canDispatch,
        bool dispatchRequested,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult)
    {
        if (!canDispatch)
        {
            return "blocked";
        }

        if (executeRequested)
        {
            return "execute_blocked_ready_for_task_run";
        }

        if (hostDispatchAttempted)
        {
            return delegatedResult?.Accepted == true
                ? "dry_run_handoff_proved"
                : "dry_run_handoff_rejected";
        }

        return dispatchRequested
            ? "dispatch_requested_waiting_for_handoff"
            : "ready_for_task_run";
    }

    private static string ResolveWorkerAutomationHostLifecycleHandoffNextAction(
        bool canDispatch,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult,
        string taskRunCommand,
        string evidenceCommand)
    {
        if (!canDispatch)
        {
            return "Resolve worker automation readiness blockers before attempting task run.";
        }

        if (executeRequested)
        {
            return $"Schedule tick cannot execute the task. Run `{taskRunCommand}`, then read back `{evidenceCommand}` and report the observed run id.";
        }

        if (hostDispatchAttempted && delegatedResult?.Accepted == true)
        {
            return $"Dry-run dispatch proved the Host route. Run `{taskRunCommand}` for real execution, then read back `{evidenceCommand}`.";
        }

        if (hostDispatchAttempted)
        {
            return "Inspect delegated_result before retrying the Host lifecycle handoff.";
        }

        return $"Run `{taskRunCommand}` through Host when the schedule callback is approved, then read back `{evidenceCommand}`.";
    }

    private static JsonObject BuildWorkerAutomationScheduleTickReceipt(
        string repoId,
        string? taskId,
        bool dispatchRequested,
        bool dryRun,
        bool canDispatch,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult)
    {
        return new JsonObject
        {
            ["schema_version"] = "worker-automation-schedule-tick-receipt.v1",
            ["receipt_id"] = $"schedule-tick:{repoId}:{(string.IsNullOrWhiteSpace(taskId) ? "none" : taskId)}:{ResolveScheduleTickReceiptMode(dispatchRequested, dryRun, hostDispatchAttempted)}",
            ["host_lifecycle_handoff_id"] = $"worker-automation-handoff:{repoId}:{(string.IsNullOrWhiteSpace(taskId) ? "<task-id>" : taskId)}",
            ["repo_id"] = repoId,
            ["task_id"] = taskId,
            ["dispatch_requested"] = dispatchRequested,
            ["dry_run"] = dryRun,
            ["schedule_tick_can_dispatch"] = canDispatch,
            ["host_dispatch_attempted"] = hostDispatchAttempted,
            ["host_dispatch_accepted"] = delegatedResult?.Accepted ?? false,
            ["delegated_outcome"] = delegatedResult?.Outcome,
            ["task_status_after_delegation"] = delegatedResult?.TaskStatus,
            ["worker_run_id"] = delegatedResult?.RunId,
            ["execution_run_id"] = delegatedResult?.ExecutionRunId,
            ["result_submission_status"] = delegatedResult?.ResultSubmissionStatus,
            ["result_envelope_path"] = delegatedResult?.ResultEnvelopePath,
            ["review_submission_path"] = delegatedResult?.ReviewSubmissionPath,
            ["effect_ledger_path"] = delegatedResult?.EffectLedgerPath,
            ["safety_gate_status"] = delegatedResult?.SafetyGateStatus,
            ["safety_gate_allowed"] = delegatedResult?.SafetyGateAllowed,
            ["callback_is_evidence_only"] = true,
            ["writes_task_truth"] = false,
            ["grants_execution_authority"] = false,
            ["grants_truth_write_authority"] = false,
        };
    }

    private static JsonObject BuildWorkerAutomationCallbackResultCheck(
        string? taskId,
        bool dispatchRequested,
        bool dryRun,
        bool canDispatch,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult,
        JsonObject? evidenceReadback)
    {
        var evidenceCommand = string.IsNullOrWhiteSpace(taskId)
            ? "api worker-dispatch-pilot-evidence <task-id>"
            : $"api worker-dispatch-pilot-evidence {taskId}";
        var taskRunCommand = string.IsNullOrWhiteSpace(taskId)
            ? "task run <task-id>"
            : $"task run {taskId}";
        var evidenceChainComplete = evidenceReadback is not null
                                    && TryGetBoolean(evidenceReadback, "pilot_chain_complete");
        var hostValidation = evidenceReadback?["host_validation"]?.AsObject();
        var hostValidationValid = hostValidation is not null
                                   && TryGetBoolean(hostValidation, "valid");
        return new JsonObject
        {
            ["kind"] = "worker_automation_callback_result_check",
            ["status"] = ResolveWorkerAutomationCallbackResultCheckStatus(
                dispatchRequested,
                dryRun,
                canDispatch,
                executeRequested,
                hostDispatchAttempted,
                delegatedResult,
                evidenceChainComplete),
            ["task_id"] = taskId,
            ["host_lifecycle_handoff_required"] = true,
            ["host_lifecycle_command"] = taskRunCommand,
            ["post_run_readback_command"] = evidenceCommand,
            ["evidence_surface_ref"] = "worker-dispatch-pilot-evidence",
            ["evidence_command"] = evidenceCommand,
            ["run_id_required_after_task_run"] = true,
            ["run_id_readback_source"] = "worker-dispatch-pilot-evidence.dispatch.latest_run_id",
            ["result_run_id_readback_source"] = "worker-dispatch-pilot-evidence.result_ingestion.result_execution_run_id",
            ["host_validation_required"] = true,
            ["host_validation_readback_source"] = "worker-dispatch-pilot-evidence.host_validation.valid",
            ["host_validation_status"] = hostValidation?["status"]?.DeepClone(),
            ["host_validation_valid"] = hostValidationValid,
            ["evidence_readback_ready"] = hostDispatchAttempted
                                        && delegatedResult?.Accepted == true
                                        && !dryRun,
            ["evidence_readback_included"] = evidenceReadback is not null,
            ["evidence_chain_complete"] = evidenceChainComplete,
            ["evidence_missing_links"] = BuildWorkerAutomationEvidenceMissingLinks(evidenceReadback),
            ["host_dispatch_accepted"] = delegatedResult?.Accepted ?? false,
            ["execute_blocked_by_schedule_tick_boundary"] = executeRequested,
            ["host_result_ingestion_attempted"] = delegatedResult?.HostResultIngestionAttempted ?? false,
            ["host_result_ingestion_applied"] = delegatedResult?.HostResultIngestionApplied ?? false,
            ["result_submission_status"] = delegatedResult?.ResultSubmissionStatus,
            ["review_submission_path"] = delegatedResult?.ReviewSubmissionPath,
            ["effect_ledger_path"] = delegatedResult?.EffectLedgerPath,
            ["required_readback_links"] = new JsonArray
            {
                "host_dispatch_run",
                "worker_evidence",
                "safety_artifact",
                "result_envelope",
                "review_submission_sidecar",
                "effect_ledger",
                "review_artifact",
                "review_bundle",
                "closure_decision",
                "host_validation",
            },
            ["completion_claim_required"] = true,
            ["review_bundle_required"] = true,
            ["closure_writeback_requires_review_bundle"] = true,
            ["planner_reentry_required_after_review"] = true,
            ["worker_claim_is_not_truth"] = true,
            ["schedule_callback_is_not_completion"] = true,
            ["next_action"] = ResolveWorkerAutomationCallbackResultCheckNextAction(
                dispatchRequested,
                dryRun,
                canDispatch,
                executeRequested,
                hostDispatchAttempted,
                delegatedResult,
                evidenceChainComplete,
                evidenceCommand),
        };
    }

    private static JsonObject BuildWorkerAutomationCoordinatorCallback(string? taskId)
    {
        return new JsonObject
        {
            ["kind"] = "worker_automation_coordinator_callback_projection",
            ["required"] = true,
            ["minimum_payload_fields"] = new JsonArray
            {
                "schedule_tick_receipt",
                "task_id",
                "host_dispatch_attempted",
                "host_dispatch_accepted",
                "host_lifecycle_handoff.status",
                "host_lifecycle_handoff.task_run_command",
                "host_lifecycle_handoff.post_run_readback_command",
                "callback_result_check.status",
                "callback_result_check.evidence_command",
            },
            ["message_template"] = string.IsNullOrWhiteSpace(taskId)
                ? "Scheduled worker tick fired. Report schedule_tick_receipt, then run host_lifecycle_handoff.task_run_command and callback_result_check.evidence_command after Host dispatch."
                : $"Scheduled worker tick fired for {taskId}. Report schedule_tick_receipt, then run host_lifecycle_handoff.task_run_command and callback_result_check.evidence_command after Host dispatch.",
            ["writes_truth"] = false,
            ["approves_review"] = false,
            ["marks_task_completed"] = false,
        };
    }

    private static string ResolveScheduleTickReceiptMode(bool dispatchRequested, bool dryRun, bool hostDispatchAttempted)
    {
        if (!dispatchRequested)
        {
            return "preflight";
        }

        if (!dryRun)
        {
            return "execute-blocked";
        }

        if (!hostDispatchAttempted)
        {
            return "dispatch-blocked";
        }

        return dryRun ? "dispatch-dry-run" : "dispatch-execute";
    }

    private static string ResolveWorkerAutomationCallbackResultCheckStatus(
        bool dispatchRequested,
        bool dryRun,
        bool canDispatch,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult,
        bool evidenceChainComplete)
    {
        if (!canDispatch)
        {
            return "blocked_before_dispatch";
        }

        if (!dispatchRequested)
        {
            return "waiting_for_schedule_dispatch";
        }

        if (executeRequested)
        {
            return "execute_blocked_use_task_run";
        }

        if (!hostDispatchAttempted)
        {
            return "dispatch_not_attempted";
        }

        if (delegatedResult?.Accepted != true)
        {
            return "host_delegation_rejected";
        }

        if (dryRun)
        {
            return "dry_run_no_evidence_readback";
        }

        if (evidenceChainComplete)
        {
            return "evidence_readback_complete";
        }

        return delegatedResult.HostResultIngestionAttempted
            ? "evidence_readback_required"
            : "worker_result_pending";
    }

    private static string ResolveWorkerAutomationCallbackResultCheckNextAction(
        bool dispatchRequested,
        bool dryRun,
        bool canDispatch,
        bool executeRequested,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult,
        bool evidenceChainComplete,
        string evidenceCommand)
    {
        if (!canDispatch)
        {
            return "Resolve schedule tick blockers before dispatch.";
        }

        if (!dispatchRequested)
        {
            return "When the schedule fires, call api worker-automation-schedule-tick --dispatch and report the schedule_tick_receipt back to the coordinator.";
        }

        if (executeRequested)
        {
            return string.IsNullOrWhiteSpace(evidenceCommand)
                ? "Real worker execution is blocked on this schedule tick surface. Use task run <task-id>, then read back worker-dispatch-pilot-evidence."
                : $"Real worker execution is blocked on this schedule tick surface. Use task run for the task, then read back the worker/result/review chain with `{evidenceCommand}`.";
        }

        if (!hostDispatchAttempted || delegatedResult?.Accepted != true)
        {
            return "Inspect callback_result_check and delegated_result before retrying the scheduled Host dispatch.";
        }

        if (dryRun)
        {
            return "Dry-run dispatch proved the Host route. Use task run <task-id> for real worker execution, then read back worker-dispatch-pilot-evidence.";
        }

        if (evidenceChainComplete)
        {
            return "Evidence readback is complete; continue with the governed review decision, writeback gate, and planner re-entry.";
        }

        return $"Read back the worker/result/review chain with `{evidenceCommand}` before claiming closure.";
    }

    private static bool ShouldReadBackWorkerAutomationEvidence(
        string? taskId,
        bool dryRun,
        bool hostDispatchAttempted,
        DelegatedExecutionResultEnvelope? delegatedResult)
    {
        return !string.IsNullOrWhiteSpace(taskId)
               && !dryRun
               && hostDispatchAttempted
               && delegatedResult?.Accepted == true;
    }

    private static JsonNode BuildWorkerAutomationEvidenceMissingLinks(JsonObject? evidenceReadback)
    {
        if (evidenceReadback is null
            || evidenceReadback["completeness"] is not JsonObject completeness
            || completeness["missing_links"] is not JsonNode missingLinks)
        {
            return new JsonArray();
        }

        return missingLinks.DeepClone();
    }

    private static bool TryGetBoolean(JsonObject node, string propertyName)
    {
        return node.TryGetPropertyValue(propertyName, out var value)
               && value is not null
               && value.GetValue<bool>();
    }

    private static string? TryGetString(JsonObject node, string propertyName)
    {
        return node.TryGetPropertyValue(propertyName, out var value)
               && value is not null
            ? value.GetValue<string>()
            : null;
    }

    private sealed record WorkerAutomationReviewGateReadiness(
        int ReviewTaskCount,
        int BlockedCount,
        JsonArray Blockers);

    private sealed record ScheduleBoundWorkerSessionReadiness(
        DateTimeOffset CheckedAt,
        ActorSessionRecord[] TotalScheduleBoundWorkers,
        ActorSessionRecord[] LiveScheduleBoundWorkers,
        ActorSessionRecord[] StaleScheduleBoundWorkers,
        ActorSessionRecord[] ClosedScheduleBoundWorkers);
}
