using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildIntentStatus()
    {
        return BuildIntentStatusFrom(services.IntentDiscoveryService.GetStatus());
    }

    public JsonObject BuildPlanStatus()
    {
        var status = services.IntentDiscoveryService.GetStatus();
        return BuildPlanStatus(status);
    }

    public JsonObject BuildPlanStatusFromInitialization(string? candidateCardId)
    {
        var currentStatus = services.IntentDiscoveryService.GetStatus();
        var currentPacket = services.FormalPlanningPacketService.TryBuildCurrentPacket();
        var currentActivePlanningCard = currentStatus.Draft?.ActivePlanningCard;
        var status = currentActivePlanningCard is not null
            && currentPacket?.FormalPlanningState == FormalPlanningState.Closed
            ? services.IntentDiscoveryService.InitializeFormalPlanningAfterClosedSlot(currentActivePlanningCard.PlanningCardId, candidateCardId)
            : services.IntentDiscoveryService.InitializeFormalPlanning(candidateCardId);
        return BuildPlanStatus(status);
    }

    public JsonObject BuildPlanExportResult(string outputPath)
    {
        var export = services.IntentDiscoveryService.ExportActivePlanningCardPayload(Path.GetFullPath(outputPath));
        return new JsonObject
        {
            ["kind"] = "formal_planning_export",
            ["output_path"] = export.OutputPath,
            ["planning_card_id"] = export.PlanningCardId,
            ["planning_slot_id"] = export.PlanningSlotId,
            ["locked_doctrine_digest"] = export.LockedDoctrineDigest,
            ["status"] = BuildPlanStatus(),
        };
    }

    public JsonObject BuildPlanPacket()
    {
        return BuildPlanPacketNode(services.FormalPlanningPacketService.BuildCurrentPacket());
    }

    public JsonObject BuildPlanIssueWorkspaceResult(string taskId)
    {
        var lease = services.ManagedWorkspaceLeaseService.IssueForTask(taskId);
        return new JsonObject
        {
            ["kind"] = "managed_workspace_issue_result",
            ["lease"] = BuildManagedWorkspaceLeaseNode(lease),
            ["status"] = BuildPlanStatus(),
        };
    }

    public JsonObject BuildPlanExportPacketResult(string outputPath)
    {
        var export = services.FormalPlanningPacketService.ExportCurrentPacket(outputPath);
        return new JsonObject
        {
            ["kind"] = "formal_planning_packet_export",
            ["output_path"] = export.OutputPath,
            ["plan_handle"] = export.PlanHandle,
            ["planning_card_id"] = export.PlanningCardId,
            ["status"] = BuildPlanStatus(),
        };
    }

    private JsonObject BuildPlanStatus(IntentDiscoveryStatus status)
    {
        var draft = status.Draft;
        var activePlanningCard = draft?.ActivePlanningCard;
        var linkedCardDrafts = activePlanningCard is null
            ? Array.Empty<CardDraftRecord>()
            : services.PlanningDraftService.ListCardDrafts()
                .Where(item => MatchesPlanningLineage(item.PlanningLineage, activePlanningCard))
                .ToArray();
        var linkedTaskGraphDrafts = activePlanningCard is null
            ? Array.Empty<TaskGraphDraftRecord>()
            : services.PlanningDraftService.ListTaskGraphDrafts()
                .Where(item => MatchesPlanningLineage(item.PlanningLineage, activePlanningCard))
                .ToArray();
        var linkedTasks = activePlanningCard is null
            ? Array.Empty<TaskNode>()
            : services.TaskGraphService.Load().ListTasks()
                .Where(task => MatchesPlanningLineage(PlanningLineageMetadata.TryRead(task.Metadata), activePlanningCard))
                .ToArray();
        var packet = activePlanningCard is null
            ? null
            : services.FormalPlanningPacketService.TryBuildCurrentPacket();
        var workspaceSurface = services.ManagedWorkspaceLeaseService.BuildSurface();
        var formalPlanningState = ResolveFormalPlanningState(status, linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks);
        var formalPlanningEntry = RuntimeFormalPlanningEntryProjectionResolver.Resolve(formalPlanningState, status, packet);
        var activePlanningSlot = ActivePlanningSlotProjectionResolver.Resolve(status, packet);
        var planningCardInvariant = PlanningCardInvariantService.Evaluate(draft, activePlanningCard);
        var planningCardFillGuidance = PlanningCardFillGuidanceService.Evaluate(activePlanningCard, planningCardInvariant);
        return new JsonObject
        {
            ["kind"] = "formal_planning_status",
            ["intent_state"] = status.State.ToString().ToLowerInvariant(),
            ["planning_posture"] = draft is null ? null : JsonNamingPolicy.SnakeCaseLower.ConvertName(draft.PlanningPosture.ToString()),
            ["formal_planning_state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(formalPlanningState.ToString()),
            ["formal_planning_entry_trigger_state"] = formalPlanningEntry.TriggerState,
            ["formal_planning_entry_command"] = formalPlanningEntry.Command,
            ["formal_planning_entry_recommended_next_action"] = formalPlanningEntry.RecommendedNextAction,
            ["formal_planning_entry_summary"] = formalPlanningEntry.Summary,
            ["active_planning_slot_id"] = activePlanningSlot.SlotId,
            ["active_planning_slot_state"] = activePlanningSlot.State,
            ["active_planning_slot_can_initialize"] = activePlanningSlot.CanInitializeFormalPlanning,
            ["active_planning_slot_card_id"] = activePlanningSlot.ActivePlanningCardId,
            ["active_planning_slot_plan_handle"] = activePlanningSlot.PlanHandle,
            ["active_planning_slot_conflict_reason"] = activePlanningSlot.ConflictReason,
            ["active_planning_slot_remediation_action"] = activePlanningSlot.RemediationAction,
            ["planning_card_invariant_state"] = planningCardInvariant.State,
            ["planning_card_invariant_can_export_governed_truth"] = planningCardInvariant.CanExportGovernedTruth,
            ["planning_card_invariant_summary"] = planningCardInvariant.Summary,
            ["planning_card_invariant_remediation_action"] = planningCardInvariant.RemediationAction,
            ["planning_card_invariant_block_count"] = planningCardInvariant.Blocks.Count,
            ["planning_card_invariant_violation_count"] = planningCardInvariant.Violations.Count,
            ["planning_card_invariant_report"] = BuildPlanningCardInvariantReportNode(planningCardInvariant),
            ["active_planning_card_fill_state"] = planningCardFillGuidance.State,
            ["active_planning_card_fill_completion_posture"] = planningCardFillGuidance.CompletionPosture,
            ["active_planning_card_fill_ready_for_recommended_export"] = planningCardFillGuidance.ReadyForRecommendedExport,
            ["active_planning_card_fill_summary"] = planningCardFillGuidance.Summary,
            ["active_planning_card_fill_recommended_next_action"] = planningCardFillGuidance.RecommendedNextFillAction,
            ["active_planning_card_fill_next_missing_field_path"] = planningCardFillGuidance.NextMissingFieldPath,
            ["active_planning_card_fill_required_field_count"] = planningCardFillGuidance.RequiredFieldCount,
            ["active_planning_card_fill_missing_required_field_count"] = planningCardFillGuidance.MissingRequiredFieldCount,
            ["active_planning_card_fill_missing_field_paths"] = ToJsonArray(planningCardFillGuidance.MissingRequiredFields.Select(field => field.FieldPath)),
            ["active_planning_card_fill_guidance"] = BuildPlanningCardFillGuidanceNode(planningCardFillGuidance),
            ["planning_slot_id"] = activePlanningCard?.PlanningSlotId,
            ["plan_handle"] = packet?.PlanHandle,
            ["focus_card_id"] = draft?.FocusCardId,
            ["recommended_next_action"] = packet?.Briefing.RecommendedNextAction ?? ResolveFormalPlanningNextAction(formalPlanningState, status),
            ["rationale"] = packet?.Briefing.Rationale ?? ResolveFormalPlanningRationale(formalPlanningState, status, linkedTasks),
            ["packet_available"] = packet is not null,
            ["packet_briefing"] = BuildPlanPacketBriefingNode(packet),
            ["managed_workspace_posture"] = workspaceSurface.OverallPosture,
            ["managed_workspace_next_action"] = workspaceSurface.RecommendedNextAction,
            ["workspace_leases"] = new JsonArray(workspaceSurface.ActiveLeases.Select(BuildManagedWorkspaceLeaseNode).ToArray()),
            ["active_planning_card"] = BuildActivePlanningCardNode(draft, activePlanningCard),
            ["lineage"] = new JsonObject
            {
                ["card_draft_ids"] = ToJsonArray(linkedCardDrafts.Select(item => item.CardId)),
                ["taskgraph_draft_ids"] = ToJsonArray(linkedTaskGraphDrafts.Select(item => item.DraftId)),
                ["task_ids"] = ToJsonArray(linkedTasks.Select(item => item.TaskId)),
            },
        };
    }

    public JsonObject BuildProtocolStatus()
    {
        var status = services.ConversationProtocolService.GetStatus(services.DevLoopService.GetSession());
        return new JsonObject
        {
            ["kind"] = "conversation_protocol",
            ["phase"] = status.CurrentPhase.ToString().ToLowerInvariant(),
            ["allowed_next_phases"] = ToJsonArray(status.AllowedNextPhases.Select(phase => phase.ToString().ToLowerInvariant())),
            ["recommended_next_action"] = status.RecommendedNextAction,
            ["rationale"] = status.Rationale,
            ["delegation_rule"] = "Inspect and delegate execution through CARVES Host before reviewing results.",
            ["delegation_commands"] = ToJsonArray(["inspect task <task-id>", "task run <task-id>"]),
        };
    }

    public JsonObject BuildProtocolCheck(string phaseValue)
    {
        if (!Enum.TryParse<ConversationPhase>(phaseValue, true, out var requestedPhase))
        {
            throw new InvalidOperationException($"Unknown conversation phase '{phaseValue}'.");
        }

        var validation = services.ConversationProtocolService.ValidateRequestedPhase(requestedPhase, services.DevLoopService.GetSession());
        return new JsonObject
        {
            ["kind"] = "conversation_protocol_check",
            ["allowed"] = validation.Allowed,
            ["current_phase"] = validation.CurrentPhase.ToString().ToLowerInvariant(),
            ["requested_phase"] = validation.RequestedPhase.ToString().ToLowerInvariant(),
            ["message"] = validation.Message,
            ["recommended_next_action"] = validation.RecommendedNextAction,
        };
    }

    public JsonObject BuildPromptKernel()
    {
        var kernel = services.PromptKernelService.GetKernel();
        return new JsonObject
        {
            ["kind"] = "prompt_kernel",
            ["kernel_id"] = kernel.KernelId,
            ["version"] = kernel.Version,
            ["source_path"] = kernel.SourcePath,
            ["supported_roles"] = ToJsonArray(kernel.SupportedRoles),
            ["summary"] = kernel.Summary,
            ["body"] = kernel.Body,
        };
    }

    public JsonObject BuildPromptTemplates()
    {
        var templates = services.PromptProtocolService.GetTemplates();
        return new JsonObject
        {
            ["kind"] = "prompt_templates",
            ["templates"] = new JsonArray(templates.Select(template => new JsonObject
            {
                ["template_id"] = template.TemplateId,
                ["version"] = template.Version,
                ["context"] = template.Context,
                ["source_path"] = template.SourcePath,
                ["sections"] = ToJsonArray(template.Sections),
                ["summary"] = template.Summary,
            }).ToArray()),
        };
    }

    public JsonObject BuildPromptTemplate(string templateId)
    {
        var template = services.PromptProtocolService.GetTemplate(templateId);
        return new JsonObject
        {
            ["kind"] = "prompt_template",
            ["template_id"] = template.TemplateId,
            ["version"] = template.Version,
            ["context"] = template.Context,
            ["source_path"] = template.SourcePath,
            ["sections"] = ToJsonArray(template.Sections),
            ["summary"] = template.Summary,
            ["body"] = template.Body,
        };
    }

    public AgentResponseEnvelope HandleAgent(AgentRequestEnvelope request)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var actorIdentity = string.IsNullOrWhiteSpace(request.ActorIdentity) ? "agent-gateway" : request.ActorIdentity;
        var actorSession = services.ActorSessionService.Ensure(
            ActorSessionKind.Agent,
            actorIdentity,
            ResolveRepoId(null),
            $"Agent request {request.OperationClass}:{request.Operation} received.",
            actorSessionId: request.ActorSessionId,
            runtimeSessionId: services.DevLoopService.GetSession()?.SessionId,
            operationClass: request.OperationClass,
            operation: request.Operation);
        RecordAgentGatewayRequestEvent(request, actorSession, receivedAt);

        var response = (request.OperationClass.ToLowerInvariant(), request.Operation.ToLowerInvariant()) switch
        {
            ("query", "status") => Accept(actorSession, "query_status", "Host status queried.", BuildAgentStatusContext()),
            ("query", "planner") => Accept(actorSession, "query_planner", "Planner state queried.", BuildDiscussionPlanner()),
            ("query", "blocked") => Accept(actorSession, "query_blocked", "Blocked summary queried.", BuildDiscussionBlocked()),
            ("query", "workers") => Accept(actorSession, "query_workers", "Worker selection queried.", BuildWorkerSelectionSummary()),
            ("query", "approvals") => Accept(actorSession, "query_approvals", "Worker approval state queried.", BuildWorkerApprovals()),
            ("query", "actors") => Accept(actorSession, "query_actors", "Actor sessions queried.", BuildActorSessions()),
            ("query", "actor-fallback-policy") or ("query", "actor_fallback_policy") => Accept(actorSession, "query_actor_fallback_policy", "Actor session fallback policy queried.", BuildActorSessionFallbackPolicy(ResolveAgentRepoId(request))),
            ("query", "actor-role-readiness") or ("query", "actor_role_readiness") => Accept(actorSession, "query_actor_role_readiness", "Actor role binding readiness queried.", BuildActorRoleReadiness(ResolveAgentRepoId(request))),
            ("query", "worker-automation-readiness") or ("query", "worker_automation_readiness") => Accept(actorSession, "query_worker_automation_readiness", "Worker automation runtime readiness queried.", BuildWorkerAutomationReadiness(ResolveAgentRepoId(request))),
            ("query", "ownership") => Accept(actorSession, "query_ownership", "Ownership bindings queried.", BuildOwnershipBindings()),
            ("query", "events") => Accept(actorSession, "query_events", "Operator OS events queried.", BuildOperatorOsEvents()),
            ("query", "agent-gateway-trace") or ("query", "agent_gateway_trace") or ("query", "agent-trace") or ("query", "agent_trace") => Accept(actorSession, "query_agent_gateway_trace", "Agent gateway trace queried.", BuildAgentGatewayTrace()),
            ("query", "intent") => Accept(actorSession, "query_intent", "Intent state queried.", BuildIntentStatus()),
            ("query", "plan") => Accept(actorSession, "query_plan", "Formal planning state queried.", BuildPlanStatus()),
            ("query", "plan_packet") => Accept(actorSession, "query_plan_packet", "Formal planning packet queried.", BuildPlanPacket()),
            ("query", "protocol") => Accept(actorSession, "query_protocol", "Conversation protocol queried.", BuildProtocolStatus()),
            ("query", "prompt") => Accept(actorSession, "query_prompt", "Prompt kernel queried.", BuildPromptKernel()),
            ("query", "card") when !string.IsNullOrWhiteSpace(request.TargetId) => Accept(actorSession, "query_card", $"Card {request.TargetId} queried.", BuildCardInspect(request.TargetId)),
            ("query", "task") when !string.IsNullOrWhiteSpace(request.TargetId) => Accept(actorSession, "query_task", $"Task {request.TargetId} queried.", BuildTaskInspect(request.TargetId)),
            ("request", "intent_draft") => Accept(actorSession, "intent_draft", "Intent draft requested.", BuildIntentDraftRequestPayload(request)),
            ("request", "plan_init") => Accept(actorSession, "plan_init", "Formal planning initialization accepted.", BuildPlanStatusFromInitialization(request.TargetId ?? request.Arguments?["candidate_card_id"]?.GetValue<string>())),
            ("request", "run_task") when !string.IsNullOrWhiteSpace(request.TargetId) => Accept(
                actorSession,
                "run_task",
                $"Delegated task run accepted for {request.TargetId}.",
                JsonSerializer.SerializeToNode(
                    services.OperatorSurfaceService.RunDelegatedTask(
                        request.TargetId,
                        request.Arguments?["dry_run"]?.GetValue<bool>() ?? false,
                        ActorSessionKind.Agent,
                        actorIdentity,
                        request.Arguments?["manual_fallback"]?.GetValue<bool>() ?? false),
                    JsonOptions)),
            ("request", "planner_wake") => Accept(actorSession, "planner_wake", "Planner wake request accepted.", BuildActionResult(services.OperatorSurfaceService.PlannerWake(PlannerWakeReason.ExplicitHumanWake, "Planner wake requested through agent gateway."))),
            ("request", "runtime_pause") => Accept(actorSession, "runtime_pause", "Runtime pause request accepted.", BuildActionResult(services.OperatorSurfaceService.PauseSession("Paused through agent gateway request."))),
            ("request", "runtime_stop") => Accept(actorSession, "runtime_stop", "Runtime stop request accepted.", BuildActionResult(services.OperatorSurfaceService.StopSession("Stopped through agent gateway request."))),
            ("report", _) => Accept(actorSession, "report", PersistAgentReport(request)),
            _ => new AgentResponseEnvelope(false, "rejected", $"Unsupported agent gateway operation '{request.OperationClass}:{request.Operation}'.", actorSession.ActorSessionId, null),
        };

        var respondedAt = DateTimeOffset.UtcNow;
        var traced = response with
        {
            RequestId = request.RequestId,
            OperationClass = request.OperationClass,
            Operation = request.Operation,
            TargetId = request.TargetId,
            ReceivedAt = receivedAt,
            RespondedAt = respondedAt,
        };
        RecordAgentGatewayResponseEvent(request, actorSession, traced, respondedAt);
        return traced;
    }

    public JsonObject BuildAgentGatewayTrace()
    {
        var events = services.OperatorApiService.GetOperatorOsEvents()
            .Where(item => item.EventKind is OperatorOsEventKind.AgentGatewayRequestReceived
                or OperatorOsEventKind.AgentGatewayResponseReturned)
            .Take(100)
            .ToArray();

        return new JsonObject
        {
            ["kind"] = "agent_gateway_trace",
            ["event_count"] = events.Length,
            ["retention"] = "recent_operator_os_events",
            ["request_feedback_visible"] = true,
            ["events"] = new JsonArray(events.Select(item => new JsonObject
            {
                ["event_id"] = item.EventId,
                ["event_kind"] = item.EventKind.ToString(),
                ["request_id"] = item.ReferenceId,
                ["actor_session_id"] = item.ActorSessionId,
                ["actor_identity"] = item.ActorIdentity,
                ["reason_code"] = item.ReasonCode,
                ["summary"] = item.Summary,
                ["occurred_at"] = item.OccurredAt,
            }).ToArray()),
        };
    }

    private void RecordAgentGatewayRequestEvent(AgentRequestEnvelope request, ActorSessionRecord actorSession, DateTimeOffset occurredAt)
    {
        services.OperatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.AgentGatewayRequestReceived,
            RepoId = ResolveAgentRepoId(request),
            ActorSessionId = actorSession.ActorSessionId,
            ActorKind = actorSession.Kind,
            ActorIdentity = actorSession.ActorIdentity,
            ReferenceId = request.RequestId,
            ReasonCode = "agent_gateway_request_received",
            Summary = $"Agent gateway received {request.OperationClass}:{request.Operation}{FormatAgentGatewayTarget(request.TargetId)}.",
            OccurredAt = occurredAt,
        });
    }

    private void RecordAgentGatewayResponseEvent(
        AgentRequestEnvelope request,
        ActorSessionRecord actorSession,
        AgentResponseEnvelope response,
        DateTimeOffset occurredAt)
    {
        services.OperatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.AgentGatewayResponseReturned,
            RepoId = ResolveAgentRepoId(request),
            ActorSessionId = actorSession.ActorSessionId,
            ActorKind = actorSession.Kind,
            ActorIdentity = actorSession.ActorIdentity,
            ReferenceId = request.RequestId,
            ReasonCode = response.Accepted ? "agent_gateway_response_accepted" : "agent_gateway_response_rejected",
            Summary = $"Agent gateway returned {response.Outcome} for {request.OperationClass}:{request.Operation}{FormatAgentGatewayTarget(request.TargetId)}. {response.Message}",
            OccurredAt = occurredAt,
        });
    }

    private static string FormatAgentGatewayTarget(string? targetId)
    {
        return string.IsNullOrWhiteSpace(targetId)
            ? string.Empty
            : $" target={targetId.Trim()}";
    }

    public JsonObject BuildActorSessions()
    {
        return new JsonObject
        {
            ["kind"] = "actor_sessions",
            ["sessions"] = new JsonArray(services.OperatorApiService.GetActorSessions().Select(item => new JsonObject
            {
                ["actor_session_id"] = item.ActorSessionId,
                ["kind"] = item.Kind.ToString(),
                ["state"] = item.State.ToString(),
                ["actor_identity"] = item.ActorIdentity,
                ["repo_id"] = item.RepoId,
                ["runtime_session_id"] = item.RuntimeSessionId,
                ["process_id"] = item.ProcessId,
                ["process_started_at"] = item.ProcessStartedAt,
                ["process_tracking_status"] = ActorSessionLivenessRules.ResolveProcessTrackingStatus(item),
                ["provider_profile"] = item.ProviderProfile,
                ["capability_profile"] = item.CapabilityProfile,
                ["session_scope"] = item.SessionScope,
                ["budget_profile"] = item.BudgetProfile,
                ["schedule_binding"] = item.ScheduleBinding,
                ["last_context_receipt"] = item.LastContextReceipt,
                ["lease_eligible"] = item.LeaseEligible,
                ["health_posture"] = item.HealthPosture,
                ["last_operation_class"] = item.LastOperationClass,
                ["last_operation"] = item.LastOperation,
                ["role_boundary"] = ResolveActorSessionRoleBoundary(item.Kind),
                ["decision_authority"] = item.Kind == ActorSessionKind.Planner,
                ["implementation_authority"] = item.Kind == ActorSessionKind.Worker,
                ["host_dispatch_required"] = true,
                ["task_truth_write_allowed"] = false,
                ["starts_run"] = false,
                ["issues_lease"] = false,
                ["current_task_id"] = item.CurrentTaskId,
                ["current_run_id"] = item.CurrentRunId,
                ["ownership_scope"] = item.CurrentOwnershipScope?.ToString(),
                ["ownership_target_id"] = item.CurrentOwnershipTargetId,
                ["last_reason"] = item.LastReason,
            }).ToArray()),
        };
    }

    public JsonObject BuildActorSessionFallbackPolicy(string? repoId = null)
    {
        var resolvedRepoId = ResolveRepoId(repoId);
        var policy = services.ActorSessionService.BuildFallbackPolicy(resolvedRepoId);
        var node = JsonSerializer.SerializeToNode(policy, JsonOptions)?.AsObject() ?? new JsonObject();
        node["kind"] = "actor_session_fallback_policy";
        return node;
    }

    public JsonObject BuildActorRoleReadiness(string? repoId = null)
    {
        var resolvedRepoId = ResolveRepoId(repoId);
        var checkedAt = DateTimeOffset.UtcNow;
        var registeredSessions = services.OperatorApiService.GetActorSessions()
            .Where(item => string.Equals(item.RepoId, resolvedRepoId, StringComparison.Ordinal))
            .Where(item => item.State != ActorSessionState.Stopped)
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var liveness = ActorSessionLivenessRules.Classify(registeredSessions, checkedAt);
        var closedSessions = liveness.ClosedSessions;
        var staleSessions = liveness.StaleSessions;
        var processAliveSessions = registeredSessions
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_alive")
            .ToArray();
        var processMissingSessions = registeredSessions
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_missing")
            .ToArray();
        var processMismatchSessions = registeredSessions
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_mismatch")
            .ToArray();
        var processIdentityUnverifiedSessions = registeredSessions
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified")
            .ToArray();
        var heartbeatOnlySessions = registeredSessions
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only")
            .ToArray();
        var nonLiveSessionIds = closedSessions
            .Concat(staleSessions)
            .Select(item => item.ActorSessionId)
            .ToHashSet(StringComparer.Ordinal);
        var sessions = liveness.LiveSessions;
        var planners = sessions.Where(item => item.Kind == ActorSessionKind.Planner).ToArray();
        var workers = sessions.Where(item => item.Kind == ActorSessionKind.Worker).ToArray();
        var scheduleBoundWorkers = workers
            .Where(item => !string.IsNullOrWhiteSpace(item.ScheduleBinding))
            .ToArray();
        var processTrackedScheduleBoundWorkers = scheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_alive")
            .ToArray();
        var processUnverifiedScheduleBoundWorkers = scheduleBoundWorkers
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) != "process_alive")
            .ToArray();
        var contextReceiptedWorkers = workers
            .Where(item => !string.IsNullOrWhiteSpace(item.LastContextReceipt))
            .ToArray();
        var explicitPlannerRegistered = planners.Length > 0;
        var explicitWorkerRegistered = workers.Length > 0;
        var missingActorRoles = BuildMissingActorRoles(explicitPlannerRegistered, explicitWorkerRegistered);
        var fallbackAllowed = missingActorRoles.Length > 0;
        var fallbackMode = fallbackAllowed
            ? "same_agent_split_role_fallback_allowed_with_evidence"
            : "explicit_actor_sessions_ready";
        var nonLiveStopCommands = BuildActorSessionStopCommands(closedSessions.Concat(staleSessions));
        var recommendedNextAction = nonLiveStopCommands.Length > 0
            ? $"Stop or refresh non-live actor session(s) before relying on explicit role binding. Next command: {nonLiveStopCommands[0]}"
            : processUnverifiedScheduleBoundWorkers.Length > 0
            ? "Re-register schedule-bound worker session(s) with process-id and process start proof before relying on worker callback projection."
            : fallbackAllowed
            ? $"Register missing actor session role(s): {string.Join(", ", missingActorRoles)}; until then, use same-agent split-role fallback only with Host-mediated role-switch evidence."
            : "Use explicit PlannerSession and WorkerSession bindings; same-agent fallback is not needed.";
        var explicitRoleBindingReady = explicitPlannerRegistered && explicitWorkerRegistered;

        return new JsonObject
        {
            ["kind"] = "actor_role_binding_readiness",
            ["repo_id"] = resolvedRepoId,
            ["readiness_type"] = "role_binding_registration_readiness",
            ["readiness_claim"] = "registered_actor_roles_only",
            ["readiness_scope"] = "repo_registered_actor_sessions",
            ["not_worker_automation_readiness"] = true,
            ["automation_readiness_status"] = "not_evaluated",
            ["host_health_checked"] = false,
            ["dispatchable_task_checked"] = false,
            ["worker_runtime_availability_checked"] = false,
            ["review_gate_checked"] = false,
            ["automation_readiness_unchecked_inputs"] = new JsonArray
            {
                "host_health",
                "dispatchable_task",
                "worker_runtime_availability",
                "review_gate_blockers",
            },
            ["registered_actor_count"] = registeredSessions.Length,
            ["live_actor_session_count"] = sessions.Count,
            ["non_live_actor_session_count"] = nonLiveSessionIds.Count,
            ["stale_actor_session_count"] = staleSessions.Count,
            ["closed_actor_session_count"] = closedSessions.Count,
            ["actor_session_freshness_window_seconds"] = (int)liveness.FreshnessWindow.TotalSeconds,
            ["actor_session_liveness_checked_at"] = checkedAt,
            ["stale_actor_session_ids"] = ToJsonArray(staleSessions.Select(item => item.ActorSessionId)),
            ["closed_actor_session_ids"] = ToJsonArray(closedSessions.Select(item => item.ActorSessionId)),
            ["process_tracked_actor_session_count"] = processAliveSessions.Length + processMissingSessions.Length + processMismatchSessions.Length + processIdentityUnverifiedSessions.Length,
            ["process_alive_actor_session_count"] = processAliveSessions.Length,
            ["process_missing_actor_session_count"] = processMissingSessions.Length,
            ["process_mismatch_actor_session_count"] = processMismatchSessions.Length,
            ["process_identity_unverified_actor_session_count"] = processIdentityUnverifiedSessions.Length,
            ["heartbeat_only_actor_session_count"] = heartbeatOnlySessions.Length,
            ["process_missing_actor_session_ids"] = ToJsonArray(processMissingSessions.Select(item => item.ActorSessionId)),
            ["process_mismatch_actor_session_ids"] = ToJsonArray(processMismatchSessions.Select(item => item.ActorSessionId)),
            ["process_identity_unverified_actor_session_ids"] = ToJsonArray(processIdentityUnverifiedSessions.Select(item => item.ActorSessionId)),
            ["heartbeat_only_actor_session_ids"] = ToJsonArray(heartbeatOnlySessions.Select(item => item.ActorSessionId)),
            ["actor_session_process_tracking_policy"] = "process_id_and_process_started_at_required_for_process_alive; heartbeat_only_sessions_depend_on_freshness_window",
            ["non_live_actor_session_reasons"] = ToJsonArray(closedSessions
                .Concat(staleSessions)
                .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
                .Select(item => $"{item.ActorSessionId}:{ActorSessionLivenessRules.ResolveNonLiveReason(item, checkedAt)}")),
            ["non_live_actor_stop_commands"] = ToJsonArray(nonLiveStopCommands),
            ["non_live_actor_session_policy"] = "stale_or_closed_sessions_do_not_satisfy_role_binding",
            ["explicit_planner_registered"] = explicitPlannerRegistered,
            ["explicit_worker_registered"] = explicitWorkerRegistered,
            ["fallback_allowed"] = fallbackAllowed,
            ["fallback_mode"] = fallbackMode,
            ["missing_actor_roles"] = ToJsonArray(missingActorRoles),
            ["planner_session_ids"] = ToJsonArray(planners.Select(item => item.ActorSessionId)),
            ["worker_session_ids"] = ToJsonArray(workers.Select(item => item.ActorSessionId)),
            ["schedule_bound_worker_session_ids"] = ToJsonArray(scheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["process_tracked_schedule_bound_worker_session_ids"] = ToJsonArray(processTrackedScheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["process_unverified_schedule_bound_worker_session_ids"] = ToJsonArray(processUnverifiedScheduleBoundWorkers.Select(item => item.ActorSessionId)),
            ["schedule_bound_worker_process_tracking_ready"] = processTrackedScheduleBoundWorkers.Length > 0,
            ["context_receipted_worker_session_ids"] = ToJsonArray(contextReceiptedWorkers.Select(item => item.ActorSessionId)),
            ["explicit_role_binding_ready"] = explicitRoleBindingReady,
            ["explicit_role_binding_ready_policy"] = "planner_and_worker_actor_sessions_registered_and_live; does_not_prove_worker_automation_dispatch_readiness",
            ["explicit_dispatch_ready"] = explicitRoleBindingReady,
            ["explicit_dispatch_ready_deprecated"] = true,
            ["explicit_dispatch_ready_replaced_by"] = "explicit_role_binding_ready",
            ["explicit_dispatch_ready_semantics"] = "deprecated_alias_for_role_binding_only_not_worker_automation_dispatch",
            ["planner_reentry_ready"] = planners.Length > 0,
            ["worker_callback_registration_present"] = scheduleBoundWorkers.Length > 0,
            ["worker_callback_projection_ready"] = processTrackedScheduleBoundWorkers.Length > 0,
            ["worker_callback_projection_policy"] = "schedule_bound_worker_requires_process_alive_before_callback_projection_is_ready",
            ["recommended_next_action"] = recommendedNextAction,
            ["grants_execution_authority"] = false,
            ["grants_truth_write_authority"] = false,
            ["creates_task_queue"] = false,
            ["agent_gateway_query_only"] = true,
            ["usage_note"] = "This readiness surface only reports role-binding registration posture. It is not proof that worker automation can run now.",
        };
    }

    private static string[] BuildActorSessionStopCommands(IEnumerable<ActorSessionRecord> sessions)
    {
        return ActorSessionLivenessRules.BuildStopCommands(sessions, "actor-thread-not-live");
    }

    private string ResolveAgentRepoId(AgentRequestEnvelope request)
    {
        if (request.Arguments is not null
            && request.Arguments.TryGetPropertyValue("repo_id", out var repoIdNode)
            && repoIdNode is not null)
        {
            var repoId = repoIdNode.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(repoId))
            {
                return repoId.Trim();
            }
        }

        return ResolveRepoId(null);
    }

    private string ResolveRepoId(string? repoId)
    {
        if (!string.IsNullOrWhiteSpace(repoId))
        {
            return repoId.Trim();
        }

        var platformRepoId = services.OperatorApiService.GetPlatformStatus().Repos.FirstOrDefault()?.RepoId;
        if (!string.IsNullOrWhiteSpace(platformRepoId))
        {
            return platformRepoId;
        }

        return Path.GetFileName(services.Paths.RepoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string ResolveActorSessionRoleBoundary(ActorSessionKind kind)
    {
        return kind switch
        {
            ActorSessionKind.Operator => "operator_governance_shell_no_task_queue",
            ActorSessionKind.Planner => "planner_decision_only_no_implementation",
            ActorSessionKind.Worker => "worker_execution_only_host_dispatch_required",
            ActorSessionKind.Agent => "agent_attached_unbound_until_role_binding",
            _ => "actor_session_unclassified",
        };
    }

    private static string[] BuildMissingActorRoles(bool explicitPlannerRegistered, bool explicitWorkerRegistered)
    {
        var roles = new List<string>();
        if (!explicitPlannerRegistered)
        {
            roles.Add("planner");
        }

        if (!explicitWorkerRegistered)
        {
            roles.Add("worker");
        }

        return roles.ToArray();
    }

    public JsonObject BuildOwnershipBindings()
    {
        return new JsonObject
        {
            ["kind"] = "ownership_bindings",
            ["bindings"] = new JsonArray(services.OperatorApiService.GetOwnershipBindings().Select(item => new JsonObject
            {
                ["binding_id"] = item.BindingId,
                ["scope"] = item.Scope.ToString(),
                ["target_id"] = item.TargetId,
                ["owner_actor_session_id"] = item.OwnerActorSessionId,
                ["owner_kind"] = item.OwnerKind.ToString(),
                ["owner_identity"] = item.OwnerIdentity,
                ["reason"] = item.Reason,
                ["claimed_at"] = item.ClaimedAt,
            }).ToArray()),
        };
    }

    public JsonObject BuildOperatorOsEvents()
    {
        return new JsonObject
        {
            ["kind"] = "operator_os_events",
            ["events"] = new JsonArray(services.OperatorApiService.GetOperatorOsEvents().Take(100).Select(item => new JsonObject
            {
                ["event_id"] = item.EventId,
                ["event_kind"] = item.EventKind.ToString(),
                ["repo_id"] = item.RepoId,
                ["actor_session_id"] = item.ActorSessionId,
                ["actor_kind"] = item.ActorKind?.ToString(),
                ["actor_identity"] = item.ActorIdentity,
                ["task_id"] = item.TaskId,
                ["run_id"] = item.RunId,
                ["permission_request_id"] = item.PermissionRequestId,
                ["ownership_scope"] = item.OwnershipScope?.ToString(),
                ["ownership_target_id"] = item.OwnershipTargetId,
                ["reason_code"] = item.ReasonCode,
                ["summary"] = item.Summary,
                ["detail_ref"] = item.DetailRef,
                ["detail_hash"] = item.DetailHash,
                ["excerpt_tail"] = item.ExcerptTail,
                ["original_summary_length"] = item.OriginalSummaryLength,
                ["summary_truncated"] = item.SummaryTruncated,
                ["occurred_at"] = item.OccurredAt,
            }).ToArray()),
        };
    }

    private string PersistAgentReport(AgentRequestEnvelope request)
    {
        var path = LocalHostPaths.GetAgentGatewayReportsPath(services.Paths.RepoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonArray ?? new JsonArray()
            : new JsonArray();
        existing.Add(new JsonObject
        {
            ["recorded_at"] = DateTimeOffset.UtcNow,
            ["request_id"] = request.RequestId,
            ["operation"] = request.Operation,
            ["target_id"] = request.TargetId,
            ["arguments"] = request.Arguments?.DeepClone(),
            ["payload"] = request.Payload?.DeepClone(),
        });
        File.WriteAllText(path, existing.ToJsonString(JsonOptions));
        return $"Agent report '{request.Operation}' was recorded.";
    }

    private static AgentResponseEnvelope Accept(ActorSessionRecord actorSession, string outcome, string message, JsonNode? payload = null)
    {
        return new AgentResponseEnvelope(true, outcome, message, actorSession.ActorSessionId, payload);
    }

    internal static JsonObject BuildIntentStatusFrom(IntentDiscoveryStatus status)
    {
        var planningCardInvariant = PlanningCardInvariantService.Evaluate(status.Draft, status.Draft?.ActivePlanningCard);
        var planningCardFillGuidance = PlanningCardFillGuidanceService.Evaluate(status.Draft?.ActivePlanningCard, planningCardInvariant);
        return new JsonObject
        {
            ["kind"] = "intent_status",
            ["state"] = status.State.ToString().ToLowerInvariant(),
            ["accepted_intent_path"] = status.AcceptedIntentPath,
            ["accepted_intent_exists"] = status.AcceptedIntentExists,
            ["accepted_intent_preview"] = status.AcceptedIntentPreview,
            ["acceptance_required"] = status.AcceptanceRequired,
            ["recommended_next_action"] = status.RecommendedNextAction,
            ["rationale"] = status.Rationale,
            ["draft"] = BuildIntentDraftNode(status.Draft, planningCardInvariant, planningCardFillGuidance),
        };
    }

    internal static JsonObject BuildIntentPreviewFrom(IntentDiscoveryStatus status)
    {
        var planningCardInvariant = PlanningCardInvariantService.Evaluate(status.Draft, status.Draft?.ActivePlanningCard);
        var planningCardFillGuidance = PlanningCardFillGuidanceService.Evaluate(status.Draft?.ActivePlanningCard, planningCardInvariant);
        return new JsonObject
        {
            ["kind"] = "intent_preview",
            ["preview_state"] = "non_durable_candidate",
            ["accepted_intent_path"] = status.AcceptedIntentPath,
            ["accepted_intent_exists"] = status.AcceptedIntentExists,
            ["accepted_intent_preview"] = status.AcceptedIntentPreview,
            ["recommended_next_action"] = status.RecommendedNextAction,
            ["rationale"] = status.Rationale,
            ["preview_only"] = true,
            ["mutated"] = false,
            ["legacy_stateful_behavior_blocked"] = true,
            ["persist_required_for_durable_draft"] = true,
            ["persist_command"] = "carves intent draft --persist",
            ["preview"] = BuildIntentDraftNode(status.Draft, planningCardInvariant, planningCardFillGuidance),
        };
    }

    private JsonObject BuildIntentDraftRequestPayload(AgentRequestEnvelope request)
    {
        var persist = request.Arguments?["persist"]?.GetValue<bool>() == true;
        if (persist)
        {
            return BuildIntentStatusFrom(services.IntentDiscoveryService.GenerateDraft());
        }

        return BuildIntentPreviewFrom(services.IntentDiscoveryService.PreviewDraft());
    }

    private static JsonObject? BuildIntentDraftNode(
        IntentDiscoveryDraft? draft,
        PlanningCardInvariantReport planningCardInvariant,
        PlanningCardFillGuidanceReport planningCardFillGuidance)
    {
        if (draft is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["draft_id"] = draft.DraftId,
            ["project_name"] = draft.ProjectName,
            ["purpose"] = draft.Purpose,
            ["users"] = ToJsonArray(draft.Users),
            ["core_capabilities"] = ToJsonArray(draft.CoreCapabilities),
            ["technology_scope"] = ToJsonArray(draft.TechnologyScope),
            ["source_summary"] = draft.SourceSummary,
            ["suggested_markdown"] = draft.SuggestedMarkdown,
            ["planning_posture"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(draft.PlanningPosture.ToString()),
            ["formal_planning"] = new JsonObject
            {
                ["state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(draft.FormalPlanningState.ToString()),
                ["planning_slot_id"] = draft.ActivePlanningCard?.PlanningSlotId,
                ["active_planning_card_id"] = draft.ActivePlanningCard?.PlanningCardId,
                ["plan_handle"] = draft.ActivePlanningCard is null
                    ? null
                    : FormalPlanningPacketService.BuildPlanHandle(draft.ActivePlanningCard),
                ["locked_doctrine_digest"] = draft.ActivePlanningCard?.LockedDoctrine.Digest,
                ["planning_card_invariant_state"] = planningCardInvariant.State,
                ["planning_card_invariant_can_export_governed_truth"] = planningCardInvariant.CanExportGovernedTruth,
                ["planning_card_invariant_remediation_action"] = planningCardInvariant.RemediationAction,
                ["planning_card_invariant_violation_count"] = planningCardInvariant.Violations.Count,
                ["active_planning_card_fill_state"] = planningCardFillGuidance.State,
                ["active_planning_card_fill_missing_required_field_count"] = planningCardFillGuidance.MissingRequiredFieldCount,
                ["active_planning_card_fill_recommended_next_action"] = planningCardFillGuidance.RecommendedNextFillAction,
            },
            ["focus_card_id"] = draft.FocusCardId,
            ["scope_frame"] = new JsonObject
            {
                ["goal"] = draft.ScopeFrame.Goal,
                ["first_users"] = ToJsonArray(draft.ScopeFrame.FirstUsers),
                ["validation_artifact"] = draft.ScopeFrame.ValidationArtifact,
                ["must_have"] = ToJsonArray(draft.ScopeFrame.MustHave),
                ["nice_to_have"] = ToJsonArray(draft.ScopeFrame.NiceToHave),
                ["not_now"] = ToJsonArray(draft.ScopeFrame.NotNow),
                ["constraints"] = ToJsonArray(draft.ScopeFrame.Constraints),
                ["open_questions"] = ToJsonArray(draft.ScopeFrame.OpenQuestions),
            },
            ["pending_decisions"] = new JsonArray(draft.PendingDecisions.Select(item => new JsonObject
            {
                ["decision_id"] = item.DecisionId,
                ["title"] = item.Title,
                ["why_it_matters"] = item.WhyItMatters,
                ["options"] = ToJsonArray(item.Options),
                ["current_recommendation"] = item.CurrentRecommendation,
                ["blocking_level"] = item.BlockingLevel,
                ["status"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(item.Status.ToString()),
            }).ToArray()),
            ["candidate_cards"] = new JsonArray(draft.CandidateCards.Select(item => new JsonObject
            {
                ["candidate_card_id"] = item.CandidateCardId,
                ["title"] = item.Title,
                ["summary"] = item.Summary,
                ["planning_posture"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(item.PlanningPosture.ToString()),
                ["writeback_eligibility"] = item.WritebackEligibility,
                ["focus_questions"] = ToJsonArray(item.FocusQuestions),
                ["allowed_user_actions"] = ToJsonArray(item.AllowedUserActions),
            }).ToArray()),
            ["generated_at"] = draft.GeneratedAt,
            ["recommended_next_action"] = draft.RecommendedNextAction,
        };
    }

    private static JsonObject? BuildActivePlanningCardNode(IntentDiscoveryDraft? draft, ActivePlanningCard? activePlanningCard)
    {
        if (activePlanningCard is null)
        {
            return null;
        }

        var planningCardInvariant = PlanningCardInvariantService.Evaluate(draft, activePlanningCard);
        var planningCardFillGuidance = PlanningCardFillGuidanceService.Evaluate(activePlanningCard, planningCardInvariant);
        return new JsonObject
        {
            ["planning_card_id"] = activePlanningCard.PlanningCardId,
            ["planning_slot_id"] = activePlanningCard.PlanningSlotId,
            ["plan_handle"] = FormalPlanningPacketService.BuildPlanHandle(activePlanningCard),
            ["source_intent_draft_id"] = activePlanningCard.SourceIntentDraftId,
            ["source_candidate_card_id"] = activePlanningCard.SourceCandidateCardId,
            ["state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(activePlanningCard.State.ToString()),
            ["locked_doctrine"] = new JsonObject
            {
                ["literal_lines"] = ToJsonArray(activePlanningCard.LockedDoctrine.LiteralLines),
                ["compare_rule"] = activePlanningCard.LockedDoctrine.CompareRule,
                ["digest"] = activePlanningCard.LockedDoctrine.Digest,
            },
            ["operator_intent"] = new JsonObject
            {
                ["title"] = activePlanningCard.OperatorIntent.Title,
                ["goal"] = activePlanningCard.OperatorIntent.Goal,
                ["validation_artifact"] = activePlanningCard.OperatorIntent.ValidationArtifact,
                ["acceptance_outline"] = ToJsonArray(activePlanningCard.OperatorIntent.AcceptanceOutline),
                ["constraints"] = ToJsonArray(activePlanningCard.OperatorIntent.Constraints),
                ["non_goals"] = ToJsonArray(activePlanningCard.OperatorIntent.NonGoals),
            },
            ["agent_proposal"] = new JsonObject
            {
                ["candidate_summary"] = activePlanningCard.AgentProposal.CandidateSummary,
                ["decomposition_candidates"] = ToJsonArray(activePlanningCard.AgentProposal.DecompositionCandidates),
                ["open_questions"] = ToJsonArray(activePlanningCard.AgentProposal.OpenQuestions),
                ["suggested_next_action"] = activePlanningCard.AgentProposal.SuggestedNextAction,
            },
            ["system_derived"] = new JsonObject
            {
                ["field_classes"] = new JsonArray(activePlanningCard.SystemDerived.FieldClasses.Select(item => new JsonObject
                {
                    ["field_path"] = item.FieldPath,
                    ["ownership"] = item.Ownership,
                    ["edit_policy"] = item.EditPolicy,
                    ["compare_rule"] = item.CompareRule,
                }).ToArray()),
                ["comparison_policy_summary"] = activePlanningCard.SystemDerived.ComparisonPolicySummary,
                ["locked_doctrine_digest"] = activePlanningCard.SystemDerived.LockedDoctrineDigest,
                ["last_exported_at"] = activePlanningCard.SystemDerived.LastExportedAt,
                ["last_exported_card_payload_path"] = activePlanningCard.SystemDerived.LastExportedCardPayloadPath,
            },
            ["invariant_report"] = BuildPlanningCardInvariantReportNode(planningCardInvariant),
            ["fill_guidance"] = BuildPlanningCardFillGuidanceNode(planningCardFillGuidance),
            ["issued_at"] = activePlanningCard.IssuedAt,
            ["updated_at"] = activePlanningCard.UpdatedAt,
        };
    }

    private static JsonObject BuildPlanningCardFillGuidanceNode(PlanningCardFillGuidanceReport report)
    {
        return new JsonObject
        {
            ["state"] = report.State,
            ["completion_posture"] = report.CompletionPosture,
            ["ready_for_recommended_export"] = report.ReadyForRecommendedExport,
            ["summary"] = report.Summary,
            ["recommended_next_fill_action"] = report.RecommendedNextFillAction,
            ["next_missing_field_path"] = report.NextMissingFieldPath,
            ["required_field_count"] = report.RequiredFieldCount,
            ["missing_required_field_count"] = report.MissingRequiredFieldCount,
            ["missing_required_field_paths"] = ToJsonArray(report.MissingRequiredFields.Select(field => field.FieldPath)),
            ["fields"] = new JsonArray(report.Fields.Select(field => new JsonObject
            {
                ["field_path"] = field.FieldPath,
                ["group_id"] = field.GroupId,
                ["label"] = field.Label,
                ["required"] = field.Required,
                ["is_missing"] = field.IsMissing,
                ["summary"] = field.Summary,
                ["recommended_fill_action"] = field.RecommendedFillAction,
            }).ToArray()),
            ["missing_required_fields"] = new JsonArray(report.MissingRequiredFields.Select(field => new JsonObject
            {
                ["field_path"] = field.FieldPath,
                ["group_id"] = field.GroupId,
                ["label"] = field.Label,
                ["summary"] = field.Summary,
                ["recommended_fill_action"] = field.RecommendedFillAction,
            }).ToArray()),
        };
    }

    private static JsonObject BuildPlanningCardInvariantReportNode(PlanningCardInvariantReport report)
    {
        return new JsonObject
        {
            ["state"] = report.State,
            ["can_export_governed_truth"] = report.CanExportGovernedTruth,
            ["summary"] = report.Summary,
            ["remediation_action"] = report.RemediationAction,
            ["expected_digest"] = report.ExpectedDigest,
            ["actual_digest"] = report.ActualDigest,
            ["system_derived_digest"] = report.SystemDerivedDigest,
            ["block_count"] = report.Blocks.Count,
            ["violation_count"] = report.Violations.Count,
            ["blocks"] = new JsonArray(report.Blocks.Select(block => new JsonObject
            {
                ["block_id"] = block.BlockId,
                ["field_path"] = block.FieldPath,
                ["line_index"] = block.LineIndex,
                ["literal_text"] = block.LiteralText,
                ["ownership"] = block.Ownership,
                ["compare_rule"] = block.CompareRule,
                ["remediation_action"] = block.RemediationAction,
            }).ToArray()),
            ["violations"] = new JsonArray(report.Violations.Select(violation => new JsonObject
            {
                ["violation_kind"] = violation.ViolationKind,
                ["block_id"] = violation.BlockId,
                ["field_path"] = violation.FieldPath,
                ["expected_text"] = violation.ExpectedText,
                ["actual_text"] = violation.ActualText,
                ["summary"] = violation.Summary,
                ["remediation_action"] = violation.RemediationAction,
            }).ToArray()),
        };
    }

    private static JsonObject? BuildPlanPacketBriefingNode(FormalPlanningPacket? packet)
    {
        if (packet is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["summary"] = packet.Briefing.Summary,
            ["recommended_next_action"] = packet.Briefing.RecommendedNextAction,
            ["rationale"] = packet.Briefing.Rationale,
            ["next_action_posture"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.Briefing.NextActionPosture.ToString()),
            ["replan_required"] = packet.Briefing.ReplanRequired,
            ["acceptance_binding_state"] = packet.AcceptanceContractSummary.BindingState,
        };
    }

    private static JsonObject BuildManagedWorkspaceLeaseNode(RuntimeManagedWorkspaceLeaseSurface lease)
    {
        return new JsonObject
        {
            ["lease_id"] = lease.LeaseId,
            ["workspace_id"] = lease.WorkspaceId,
            ["task_id"] = lease.TaskId,
            ["card_id"] = lease.CardId,
            ["workspace_path"] = lease.WorkspacePath,
            ["base_commit"] = lease.BaseCommit,
            ["status"] = lease.Status,
            ["approval_posture"] = lease.ApprovalPosture,
            ["cleanup_posture"] = lease.CleanupPosture,
            ["expires_at"] = lease.ExpiresAt,
            ["allowed_writable_paths"] = ToJsonArray(lease.AllowedWritablePaths),
            ["allowed_operation_classes"] = ToJsonArray(lease.AllowedOperationClasses),
            ["allowed_tools_or_adapters"] = ToJsonArray(lease.AllowedToolsOrAdapters),
        };
    }

    private static JsonObject BuildManagedWorkspaceLeaseNode(ManagedWorkspaceLease lease)
    {
        return new JsonObject
        {
            ["lease_id"] = lease.LeaseId,
            ["workspace_id"] = lease.WorkspaceId,
            ["task_id"] = lease.TaskId,
            ["card_id"] = lease.CardId,
            ["workspace_path"] = lease.WorkspacePath,
            ["base_commit"] = lease.BaseCommit,
            ["status"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(lease.Status.ToString()),
            ["approval_posture"] = lease.ApprovalPosture,
            ["cleanup_posture"] = lease.CleanupPosture,
            ["expires_at"] = lease.ExpiresAt,
            ["allowed_writable_paths"] = ToJsonArray(lease.AllowedWritablePaths),
            ["allowed_operation_classes"] = ToJsonArray(lease.AllowedOperationClasses),
            ["allowed_tools_or_adapters"] = ToJsonArray(lease.AllowedToolsOrAdapters),
        };
    }

    private static JsonObject BuildPlanPacketNode(FormalPlanningPacket packet)
    {
        return new JsonObject
        {
            ["kind"] = "formal_planning_packet",
            ["plan_handle"] = packet.PlanHandle,
            ["planning_slot_id"] = packet.PlanningSlotId,
            ["planning_card_id"] = packet.PlanningCardId,
            ["source_intent_draft_id"] = packet.SourceIntentDraftId,
            ["source_candidate_card_id"] = packet.SourceCandidateCardId,
            ["formal_planning_state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.FormalPlanningState.ToString()),
            ["briefing"] = new JsonObject
            {
                ["summary"] = packet.Briefing.Summary,
                ["recommended_next_action"] = packet.Briefing.RecommendedNextAction,
                ["rationale"] = packet.Briefing.Rationale,
                ["next_action_posture"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.Briefing.NextActionPosture.ToString()),
                ["replan_required"] = packet.Briefing.ReplanRequired,
            },
            ["acceptance_contract_summary"] = new JsonObject
            {
                ["binding_state"] = packet.AcceptanceContractSummary.BindingState,
                ["contract_id"] = packet.AcceptanceContractSummary.ContractId,
                ["lifecycle_status"] = !packet.AcceptanceContractSummary.LifecycleStatus.HasValue
                    ? null
                    : JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.AcceptanceContractSummary.LifecycleStatus.Value.ToString()),
                ["summary_lines"] = ToJsonArray(packet.AcceptanceContractSummary.SummaryLines),
                ["gap_summary"] = packet.AcceptanceContractSummary.GapSummary,
            },
            ["constraints"] = ToJsonArray(packet.Constraints),
            ["non_goals"] = ToJsonArray(packet.NonGoals),
            ["decomposition_candidates"] = ToJsonArray(packet.DecompositionCandidates),
            ["blockers"] = ToJsonArray(packet.Blockers),
            ["evidence_expectations"] = ToJsonArray(packet.EvidenceExpectations),
            ["allowed_scope_summary"] = ToJsonArray(packet.AllowedScopeSummary),
            ["replan_rules"] = new JsonArray(packet.ReplanRules.Select(rule => new JsonObject
            {
                ["rule_id"] = rule.RuleId,
                ["trigger"] = rule.Trigger,
                ["summary"] = rule.Summary,
                ["required_action"] = rule.RequiredAction,
                ["reentry_command"] = rule.ReentryCommand,
            }).ToArray()),
            ["linked_truth"] = new JsonObject
            {
                ["card_draft_ids"] = ToJsonArray(packet.LinkedTruth.CardDraftIds),
                ["taskgraph_draft_ids"] = ToJsonArray(packet.LinkedTruth.TaskGraphDraftIds),
                ["task_ids"] = ToJsonArray(packet.LinkedTruth.TaskIds),
            },
        };
    }

    private static bool MatchesPlanningLineage(PlanningLineage? lineage, ActivePlanningCard activePlanningCard)
    {
        return lineage is not null
            && string.Equals(lineage.PlanningSlotId, activePlanningCard.PlanningSlotId, StringComparison.Ordinal)
            && string.Equals(lineage.ActivePlanningCardId, activePlanningCard.PlanningCardId, StringComparison.Ordinal);
    }

    private static FormalPlanningState ResolveFormalPlanningState(
        IntentDiscoveryStatus status,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks)
    {
        if (status.Draft?.ActivePlanningCard is null)
        {
            return status.Draft?.PlanningPosture is GuidedPlanningPosture.ReadyToPlan or GuidedPlanningPosture.Grounded
                ? FormalPlanningState.PlanInitRequired
                : FormalPlanningState.Discuss;
        }

        if (linkedTasks.Count > 0)
        {
            if (linkedTasks.Any(task => task.Status is DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait))
            {
                return FormalPlanningState.ReviewBound;
            }

            if (linkedTasks.All(task => task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Discarded or DomainTaskStatus.Superseded))
            {
                return FormalPlanningState.Closed;
            }

            return FormalPlanningState.ExecutionBound;
        }

        if (linkedTaskGraphDrafts.Count > 0 || linkedCardDrafts.Count > 0)
        {
            return FormalPlanningState.PlanBound;
        }

        return FormalPlanningState.Planning;
    }

    private static string ResolveFormalPlanningNextAction(FormalPlanningState state, IntentDiscoveryStatus status)
    {
        return state switch
        {
            FormalPlanningState.Discuss => status.RecommendedNextAction,
            FormalPlanningState.PlanInitRequired => "Run `plan init [candidate-card-id]` to issue one active planning card for formal planning.",
            FormalPlanningState.Planning => "Fill the editable planning-card fields and export a card payload through `plan export-card <json-path>`.",
            FormalPlanningState.PlanBound => "Continue card/taskgraph draft work while preserving the planning lineage on the same active planning card.",
            FormalPlanningState.ExecutionBound => "Continue task execution or inspect the bound tasks on the current active planning card.",
            FormalPlanningState.ReviewBound => "Finish review and approval on the tasks bound to the current active planning card.",
            FormalPlanningState.Closed => "Formal planning for the current active planning card is closed.",
            _ => status.RecommendedNextAction,
        };
    }

    private static string ResolveFormalPlanningRationale(FormalPlanningState state, IntentDiscoveryStatus status, IReadOnlyList<TaskNode> linkedTasks)
    {
        return state switch
        {
            FormalPlanningState.Discuss => status.Rationale,
            FormalPlanningState.PlanInitRequired => "guided planning is grounded enough for formal planning, but no active planning card exists yet",
            FormalPlanningState.Planning => "one active planning card exists and still needs export into official card truth",
            FormalPlanningState.PlanBound => "planning drafts already point back to the current active planning card",
            FormalPlanningState.ExecutionBound => $"execution truth already points back to the active planning card through {linkedTasks.Count} bound tasks",
            FormalPlanningState.ReviewBound => "bound tasks are waiting in review or approval",
            FormalPlanningState.Closed => "all bound tasks reached a terminal completion state",
            _ => status.Rationale,
        };
    }

}
