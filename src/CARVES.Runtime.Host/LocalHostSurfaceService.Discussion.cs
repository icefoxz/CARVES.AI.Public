using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static class DiscussionProjection
    {
        public static JsonObject BuildDiscussionContext(LocalHostSurfaceService owner)
        {
            var interaction = owner.services.InteractionLayerService.GetSnapshot(owner.services.DevLoopService.GetSession());
            var dashboard = owner.BuildDashboardReadModel();
            return new JsonObject
            {
                ["kind"] = "discussion_context",
                ["generated_at"] = DateTimeOffset.UtcNow,
                ["stage"] = RuntimeStageInfo.CurrentStage,
                ["protocol_mode"] = interaction.ProtocolMode,
                ["conversation_phase"] = interaction.Protocol.CurrentPhase.ToString().ToLowerInvariant(),
                ["intent"] = owner.BuildIntentStatus(),
                ["prompt"] = owner.BuildPromptKernel(),
                ["project_understanding"] = owner.ToJsonObject(interaction.ProjectUnderstanding),
                ["delegation"] = owner.BuildDelegationProtocol(),
                ["dashboard_summary"] = dashboard,
            };
        }

        public static JsonObject BuildDiscussionBriefPreview(LocalHostSurfaceService owner)
        {
            var preview = BuildIntentPreviewFrom(owner.services.IntentDiscoveryService.PreviewDraft());
            return new JsonObject
            {
                ["kind"] = "project_brief_preview",
                ["generated_at"] = DateTimeOffset.UtcNow,
                ["preview_state"] = preview["preview_state"]?.DeepClone(),
                ["preview_only"] = true,
                ["mutated"] = false,
                ["legacy_stateful_behavior_blocked"] = true,
                ["persist_required_for_durable_draft"] = true,
                ["continue_discussion_command"] = "carves discuss context",
                ["persist_command"] = preview["persist_command"]?.DeepClone(),
                ["accepted_intent_path"] = preview["accepted_intent_path"]?.DeepClone(),
                ["accepted_intent_exists"] = preview["accepted_intent_exists"]?.DeepClone(),
                ["accepted_intent_preview"] = preview["accepted_intent_preview"]?.DeepClone(),
                ["recommended_next_action"] = preview["recommended_next_action"]?.DeepClone(),
                ["rationale"] = preview["rationale"]?.DeepClone(),
                ["brief_preview"] = preview["preview"]?.DeepClone(),
            };
        }

        public static JsonObject BuildAgentStatusContext(LocalHostSurfaceService owner)
        {
            var session = owner.services.DevLoopService.GetSession();
            var graph = owner.services.TaskGraphService.Load();
            var tasks = graph.Tasks.Values.ToArray();
            var completedTaskIds = graph.CompletedTaskIds();
            var nextReadyTask = tasks
                .Where(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds))
                .OrderBy(task => task.TaskId, StringComparer.Ordinal)
                .FirstOrDefault();

            return new JsonObject
            {
                ["kind"] = "agent_status_context",
                ["generated_at"] = DateTimeOffset.UtcNow,
                ["stage"] = RuntimeStageInfo.CurrentStage,
                ["runtime_session"] = new JsonObject
                {
                    ["status"] = session?.Status.ToString().ToLowerInvariant() ?? "none",
                    ["planner_state"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState),
                    ["active_task_ids"] = ToJsonArray(session?.ActiveTaskIds ?? []),
                    ["last_reason"] = session?.LastReason ?? "N/A",
                },
                ["task_counts"] = new JsonObject
                {
                    ["pending"] = tasks.Count(task => task.Status == DomainTaskStatus.Pending),
                    ["running"] = tasks.Count(task => task.Status == DomainTaskStatus.Running),
                    ["review"] = tasks.Count(task => task.Status == DomainTaskStatus.Review),
                    ["approval_wait"] = tasks.Count(task => task.Status == DomainTaskStatus.ApprovalWait),
                    ["blocked"] = tasks.Count(task => task.Status == DomainTaskStatus.Blocked),
                    ["completed"] = tasks.Count(task => task.Status == DomainTaskStatus.Completed),
                    ["merged"] = tasks.Count(task => task.Status == DomainTaskStatus.Merged),
                    ["superseded"] = tasks.Count(task => task.Status == DomainTaskStatus.Superseded),
                    ["discarded"] = tasks.Count(task => task.Status == DomainTaskStatus.Discarded),
                },
                ["dispatch"] = new JsonObject
                {
                    ["next_ready_task_id"] = nextReadyTask?.TaskId ?? "N/A",
                    ["next_ready_card_id"] = nextReadyTask?.CardId ?? "N/A",
                    ["next_ready_title"] = nextReadyTask?.Title ?? "N/A",
                },
                ["detail_commands"] = ToJsonArray(
                [
                    "carves agent context --json",
                    "carves discuss context",
                    "carves dashboard --text",
                ]),
                ["recommended_next_action"] = nextReadyTask is null
                    ? "Use detail_commands when deeper operator or agent context is required."
                    : $"Inspect or dispatch {nextReadyTask.TaskId} through the governed task surface.",
            };
        }

        public static JsonObject BuildDiscussionPlanner(LocalHostSurfaceService owner)
        {
            var session = owner.services.DevLoopService.GetSession();
            var kernel = owner.services.PromptKernelService.GetKernel();
            return new JsonObject
            {
                ["kind"] = "planner_discussion",
                ["planner_state"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState),
                ["wake_reason"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason),
                ["pending_wake_count"] = session?.PendingPlannerWakeSignals.Count ?? 0,
                ["last_consumed_wake"] = session?.LastConsumedPlannerWakeSummary,
                ["sleep_reason"] = session is null ? "none" : PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason),
                ["lease_active"] = session?.PlannerLeaseActive ?? false,
                ["lease_mode"] = session?.PlannerLeaseMode.ToString(),
                ["lifecycle_reason"] = session?.PlannerLifecycleReason ?? "(none)",
                ["planner_round"] = session?.PlannerRound ?? 0,
                ["last_reentry_outcome"] = session?.LastPlannerReentryOutcome ?? "(none)",
                ["prompt_kernel"] = $"{kernel.KernelId}@{kernel.Version}",
                ["next_action"] = session is null ? "start a session" : RuntimeActionabilitySemantics.DescribeNextAction(session),
            };
        }

        public static JsonObject BuildDiscussionBlocked(LocalHostSurfaceService owner)
        {
            var graph = owner.services.TaskGraphService.Load();
            var completedTaskIds = graph.CompletedTaskIds();
            var pendingPermissions = owner.services.OperatorApiService.GetPendingWorkerPermissionRequests();
            var acceptanceContractGapTasks = graph.Tasks.Values
                .Where(task =>
                    task.Status == DomainTaskStatus.Pending
                    && task.CanDispatchToWorkerPool
                    && task.IsReady(completedTaskIds)
                    && AcceptanceContractExecutionGate.Evaluate(task).BlocksExecution)
                .Select(task => task.TaskId)
                .OrderBy(taskId => taskId, StringComparer.Ordinal)
                .ToArray();
            return new JsonObject
            {
                ["kind"] = "blocked_discussion",
                ["blocked_total"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Blocked),
                ["approval_wait_total"] = graph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.ApprovalWait),
                ["acceptance_contract_gap_count"] = acceptanceContractGapTasks.Length,
                ["acceptance_contract_gap_tasks"] = ToJsonArray(acceptanceContractGapTasks),
                ["blocked_reasons"] = BuildBlockedReasonCounts(graph),
                ["review_pending"] = ToJsonArray(graph.Tasks.Values.Where(task => task.Status == DomainTaskStatus.Review).Select(task => task.TaskId)),
                ["approval_wait_tasks"] = ToJsonArray(graph.Tasks.Values.Where(task => task.Status == DomainTaskStatus.ApprovalWait).Select(task => task.TaskId)),
                ["pending_permission_requests"] = ToJsonArray(pendingPermissions.Select(request => request.PermissionRequestId)),
                ["recommended_next_action"] = acceptanceContractGapTasks.Length > 0
                    ? "project a minimum acceptance contract onto blocked pending tasks before dispatch"
                    : "inspect blocked reasons and pending approvals on the existing runtime lane",
            };
        }

        public static JsonObject BuildDiscussionCard(LocalHostSurfaceService owner, string cardId)
        {
            return new JsonObject
            {
                ["kind"] = "card_discussion",
                ["card"] = owner.BuildCardInspect(cardId),
            };
        }

        public static JsonObject BuildDiscussionTask(LocalHostSurfaceService owner, string taskId)
        {
            return new JsonObject
            {
                ["kind"] = "task_discussion",
                ["task"] = owner.BuildTaskInspect(taskId, includeRuns: true),
            };
        }
    }
}
