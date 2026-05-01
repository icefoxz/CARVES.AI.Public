using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed partial class WorkbenchSurfaceService
{
    private static bool IsFinal(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded;
    }

    private static bool IsWorkbenchActionable(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Pending or DomainTaskStatus.Running or DomainTaskStatus.Testing or DomainTaskStatus.Blocked or DomainTaskStatus.ApprovalWait;
    }

    private static int RankTask(TaskNode task, IReadOnlySet<string> completedTaskIds, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        var modeExecutionEntryGate = new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task);
        if (task.Status == DomainTaskStatus.Review)
        {
            return 0;
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return 1;
        }

        if (task.Status == DomainTaskStatus.Running)
        {
            return 2;
        }

        if (task.Status == DomainTaskStatus.Pending
            && task.CanDispatchToWorkerPool
            && task.IsReady(completedTaskIds)
            && !modeExecutionEntryGate.BlocksExecution)
        {
            return 3;
        }

        if (task.Status == DomainTaskStatus.Pending
            && string.Equals(modeExecutionEntryGate.FirstBlockingCheckId, "acceptance_contract_projected", StringComparison.Ordinal))
        {
            return 4;
        }

        if (task.Status == DomainTaskStatus.Pending && modeExecutionEntryGate.BlocksExecution)
        {
            return 5;
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return 6;
        }

        return 7;
    }

    private static string ResolveCardStatus(IReadOnlyList<TaskNode> tasks, Carves.Runtime.Domain.Planning.CardDraftRecord? draft, IReadOnlySet<string> completedTaskIds, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        if (tasks.Count == 0 && draft is not null)
        {
            return draft.Status.ToString().ToLowerInvariant();
        }

        if (tasks.Count == 0)
        {
            return "undefined";
        }

        if (tasks.All(task => IsFinal(task.Status)))
        {
            return "completed";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.Review))
        {
            return "review";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.ApprovalWait))
        {
            return "approval_wait";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.Running))
        {
            return "running";
        }

        if (tasks.Any(task =>
                task.Status == DomainTaskStatus.Pending
                && task.CanDispatchToWorkerPool
                && task.IsReady(completedTaskIds)
                && !new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task).BlocksExecution))
        {
            return "ready";
        }

        return "blocked";
    }

    private static string ResolveCardNextAction(IReadOnlyList<TaskNode> tasks, IReadOnlySet<string> completedTaskIds, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        var reviewTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Review);
        if (reviewTask is not null)
        {
            return $"inspect review surface for {reviewTask.TaskId}";
        }

        var readyTask = tasks.FirstOrDefault(task =>
            task.Status == DomainTaskStatus.Pending
            && task.CanDispatchToWorkerPool
            && task.IsReady(completedTaskIds)
            && !new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task).BlocksExecution);
        if (readyTask is not null)
        {
            return $"inspect task {readyTask.TaskId}";
        }

        var pendingTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Pending);
        if (pendingTask is not null)
        {
            return $"inspect task {pendingTask.TaskId}";
        }

        var blockedTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Blocked);
        if (blockedTask is not null)
        {
            return $"inspect blocked task {blockedTask.TaskId}";
        }

        return "observe current state";
    }

    private static string ResolveCardBlockedReason(IReadOnlyList<TaskNode> tasks, IReadOnlySet<string> completedTaskIds, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        var blockedTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Blocked)
            ?? tasks.FirstOrDefault(task =>
                task.Status == DomainTaskStatus.Pending
                && !(
                    task.CanDispatchToWorkerPool
                    && task.IsReady(completedTaskIds)
                    && !new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task).BlocksExecution));
        return blockedTask is null
            ? "(none)"
            : ResolveTaskBlockedReason(blockedTask, blockedTask.Dependencies.Where(dependency => !completedTaskIds.Contains(dependency)).ToArray(), formalPlanningExecutionGateService);
    }

    private static string ResolveTaskBlockedReason(TaskNode task, IReadOnlyList<string> unresolvedDependencies, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        if (IsFinal(task.Status))
        {
            return "(none)";
        }

        if (unresolvedDependencies.Count > 0)
        {
            return $"waiting on dependencies: {string.Join(", ", unresolvedDependencies)}";
        }

        if (task.Status == DomainTaskStatus.Review)
        {
            if (string.Equals(task.Metadata.GetValueOrDefault("boundary_stopped"), "true", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(task.PlannerReview.Reason))
            {
                return task.PlannerReview.Reason;
            }

            return task.PlannerReview.DecisionStatus == ReviewDecisionStatus.ProvisionalAccepted
                ? task.PlannerReview.DecisionDebt?.Summary ?? "waiting for provisional debt closure"
                : "waiting for review";
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return "waiting for permission approval";
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return task.PlannerReview.Reason ?? task.LastWorkerSummary ?? "blocked";
        }

        if (task.Status == DomainTaskStatus.Pending)
        {
            var modeExecutionEntryGate = new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task);
            if (modeExecutionEntryGate.BlocksExecution)
            {
                return modeExecutionEntryGate.Summary;
            }
        }

        if (task.LastWorkerFailureKind == WorkerFailureKind.ApprovalRequired)
        {
            return $"worker approval required: {task.LastWorkerSummary ?? "(none)"}";
        }

        if (task.LastWorkerFailureKind != WorkerFailureKind.None)
        {
            return $"worker {task.LastWorkerFailureKind}: {task.LastWorkerSummary ?? "(none)"}";
        }

        return "(none)";
    }

    private static string ResolveTaskNextAction(TaskNode task, IReadOnlyList<string> unresolvedDependencies, FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        if (task.Status == DomainTaskStatus.Review)
        {
            if (string.Equals(task.Metadata.GetValueOrDefault("boundary_stopped"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return task.PlannerReview.FollowUpSuggestions.FirstOrDefault()
                    ?? task.Metadata.GetValueOrDefault("managed_workspace_path_policy_next_action")
                    ?? "inspect the boundary stop and replan before dispatching again";
            }

            return task.PlannerReview.DecisionStatus == ReviewDecisionStatus.ProvisionalAccepted
                ? "clear provisional debt, reopen review if needed, then approve or reject review"
                : "approve, provisionally approve, or reject review";
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return "resolve permission request";
        }

        if (unresolvedDependencies.Count > 0)
        {
            return $"wait for {string.Join(", ", unresolvedDependencies)}";
        }

        if (task.Status == DomainTaskStatus.Pending)
        {
            var modeExecutionEntryGate = new ModeExecutionEntryGateService(formalPlanningExecutionGateService).Evaluate(task);
            if (modeExecutionEntryGate.BlocksExecution)
            {
                return modeExecutionEntryGate.RecommendedNextAction;
            }

            return $"delegate through host: task run {task.TaskId}";
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return "record fail or block outcome";
        }

        if (task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged
            && task.PlannerReview.DecisionStatus is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted)
        {
            return "observe downstream tasks or reopen review";
        }

        return "observe current state";
    }

    private static IReadOnlyList<WorkbenchActionDescriptor> BuildTaskActions(TaskNode task)
    {
        if (task.Status == DomainTaskStatus.Review)
        {
            return task.PlannerReview.DecisionStatus == ReviewDecisionStatus.ProvisionalAccepted
                ? [BuildApproveAction(task.TaskId), BuildProvisionalApproveAction(task.TaskId), BuildRejectAction(task.TaskId), BuildReopenAction(task.TaskId)]
                : [BuildApproveAction(task.TaskId), BuildProvisionalApproveAction(task.TaskId), BuildRejectAction(task.TaskId)];
        }

        if (task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged
            && task.PlannerReview.DecisionStatus is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted)
        {
            return [BuildReopenAction(task.TaskId)];
        }

        if (!IsWorkbenchActionable(task.Status))
        {
            return Array.Empty<WorkbenchActionDescriptor>();
        }

        return
        [
            BuildTaskOutcomeAction("done", "Done", task.TaskId, $"review-task {task.TaskId} done <reason...>", "Record a validated completion through the shared review-task path."),
            BuildTaskOutcomeAction("fail", "Fail", task.TaskId, $"review-task {task.TaskId} fail <reason...>", "Record a failed or blocked outcome without direct truth editing."),
            BuildTaskOutcomeAction("block", "Block", task.TaskId, $"review-task {task.TaskId} block <reason...>", "Record an explicit blocked outcome through the shared review-task path."),
            BuildTaskOutcomeAction("supersede", "Supersede", task.TaskId, $"review-task {task.TaskId} superseded <reason...>", "Retire stale review lineage through the shared review-task path without marking it completed."),
        ];
    }

    private static WorkbenchActionDescriptor BuildApproveAction(string taskId)
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = "approve",
            Label = "Approve",
            TargetId = taskId,
            Command = $"approve-review {taskId} <reason...>",
            RequiresReason = true,
            Summary = "Approve a pending review through the shared review gate.",
        };
    }

    private static WorkbenchActionDescriptor BuildProvisionalApproveAction(string taskId)
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = "provisional_approve",
            Label = "Provisional Approve",
            TargetId = taskId,
            Command = $"approve-review {taskId} --provisional <reason...>",
            RequiresReason = true,
            Summary = "Record a provisional acceptance debt while keeping the task at the review boundary.",
        };
    }

    private static WorkbenchActionDescriptor BuildRejectAction(string taskId)
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = "reject",
            Label = "Reject",
            TargetId = taskId,
            Command = $"reject-review {taskId} <reason...>",
            RequiresReason = true,
            Summary = "Reject a pending review through the shared review gate.",
        };
    }

    private static WorkbenchActionDescriptor BuildReopenAction(string taskId)
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = "reopen",
            Label = "Reopen",
            TargetId = taskId,
            Command = $"reopen-review {taskId} <reason...>",
            RequiresReason = true,
            Summary = "Return accepted truth to the review boundary through the shared review gate.",
        };
    }

    private static WorkbenchActionDescriptor BuildTaskOutcomeAction(string actionId, string label, string taskId, string command, string summary)
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = actionId,
            Label = label,
            TargetId = taskId,
            Command = command,
            RequiresReason = true,
            Summary = summary,
        };
    }

    private static WorkbenchActionDescriptor BuildSyncAction()
    {
        return new WorkbenchActionDescriptor
        {
            ActionId = "sync",
            Label = "Sync",
            TargetId = "runtime",
            Command = "sync-state",
            RequiresReason = false,
            Summary = "Reconcile shared task/runtime truth after review decisions or residue cleanup.",
        };
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
