using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static string ResolveCardStatus(IReadOnlyList<TaskNode> tasks, IReadOnlySet<string> completedTaskIds)
    {
        if (tasks.Count == 0)
        {
            return "undefined";
        }

        if (tasks.All(task => task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded))
        {
            return "completed";
        }

        if (tasks.All(task => task.Status == DomainTaskStatus.Suggested))
        {
            return "planning";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.Review))
        {
            return "review";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.ApprovalWait))
        {
            return "approval_wait";
        }

        if (tasks.Any(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds)))
        {
            return "ready";
        }

        if (tasks.Any(task => task.Status == DomainTaskStatus.Running))
        {
            return "running";
        }

        return "blocked";
    }

    private static string ResolveCardNextAction(IReadOnlyList<TaskNode> tasks, IReadOnlySet<string> completedTaskIds)
    {
        var reviewTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Review);
        if (reviewTask is not null)
        {
            return $"resolve review for {reviewTask.TaskId}";
        }

        var approvalTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.ApprovalWait);
        if (approvalTask is not null)
        {
            return $"resolve permission approval for {approvalTask.TaskId}";
        }

        var readyTask = tasks.FirstOrDefault(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds));
        if (readyTask is not null)
        {
            return $"inspect task {readyTask.TaskId} then delegate via task run {readyTask.TaskId}";
        }

        var pendingTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Pending);
        if (pendingTask is not null)
        {
            return $"inspect task {pendingTask.TaskId}";
        }

        var blockedTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Blocked);
        if (blockedTask is not null)
        {
            return $"investigate blocked task {blockedTask.TaskId}";
        }

        return "observe current state";
    }

    private static string ResolveCardBlockedReason(IReadOnlyList<TaskNode> tasks, IReadOnlySet<string> completedTaskIds)
    {
        var blockedTask = tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Blocked)
            ?? tasks.FirstOrDefault(task => task.Status == DomainTaskStatus.Pending && !AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds));
        return blockedTask is null
            ? "(none)"
            : ResolveTaskBlockedReason(blockedTask, blockedTask.Dependencies.Where(dependency => !completedTaskIds.Contains(dependency)).ToArray());
    }

    private static string ResolveTaskBlockedReason(TaskNode task, IReadOnlyList<string> unresolvedDependencies)
    {
        if (IsFinalTaskStatus(task.Status))
        {
            return "(none)";
        }

        if (unresolvedDependencies.Count > 0)
        {
            return $"waiting on dependencies: {string.Join(", ", unresolvedDependencies)}";
        }

        if (task.Status == DomainTaskStatus.Review)
        {
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
            var acceptanceContractGate = AcceptanceContractExecutionGate.Evaluate(task);
            if (acceptanceContractGate.BlocksExecution)
            {
                return acceptanceContractGate.Summary;
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

    private static string ResolveDispatchStateReason(TaskNode task, string blockedReason)
    {
        return IsFinalTaskStatus(task.Status)
            ? $"task is finalized: {task.Status}"
            : blockedReason;
    }

    private static string ResolveTaskNextAction(TaskNode task, IReadOnlyList<string> unresolvedDependencies)
    {
        if (task.Status == DomainTaskStatus.Review)
        {
            return task.PlannerReview.DecisionStatus == ReviewDecisionStatus.ProvisionalAccepted
                ? "clear provisional debt, reopen review if needed, then approve or reject review"
                : "approve, provisionally approve, or reject review";
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return "approve, deny, or timeout permission request";
        }

        if (unresolvedDependencies.Count > 0)
        {
            return $"wait for {string.Join(", ", unresolvedDependencies)}";
        }

        if (task.Status == DomainTaskStatus.Pending)
        {
            var acceptanceContractGate = AcceptanceContractExecutionGate.Evaluate(task);
            if (acceptanceContractGate.BlocksExecution)
            {
                return acceptanceContractGate.RecommendedNextAction;
            }

            return $"delegate through host: task run {task.TaskId}";
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return "inspect failure or request planner wake";
        }

        if (task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged)
        {
            return task.PlannerReview.DecisionStatus is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted
                ? "observe downstream tasks or reopen review"
                : "observe downstream tasks";
        }

        return "observe current state";
    }

    private static bool IsFinalTaskStatus(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded;
    }

    private static string ResolveDispatchBlockedNextAction(TaskBlockerContext blocker, string taskId)
    {
        return blocker.BlockerScope switch
        {
            "external_review_boundary" when !string.IsNullOrWhiteSpace(blocker.BlockerTaskId) => $"resolve review for {blocker.BlockerTaskId}",
            "external_approval_boundary" => "approve, deny, or timeout permission request",
            "external_residue" when !string.IsNullOrWhiteSpace(blocker.BlockerTaskId) => $"inspect external residue for {blocker.BlockerTaskId}",
            "dependency" => blocker.Summary ?? $"wait before rerunning {taskId}",
            _ => $"inspect blocker before rerunning {taskId}",
        };
    }

    private static TaskBlockerContext ResolveTaskBlockerContext(
        TaskNode task,
        RuntimeSessionState? session,
        DelegatedRunLifecycleRecord? delegatedLifecycle,
        IReadOnlyList<DelegatedRunLifecycleRecord> lifecycleRecords,
        IReadOnlyList<string> unresolvedDependencies)
    {
        if (unresolvedDependencies.Count > 0)
        {
            return new TaskBlockerContext(
                BlocksDispatch: true,
                BlockerTaskId: null,
                BlockerLifecycleId: null,
                BlockerScope: "dependency",
                BlockedByExternalResidue: false,
                Summary: $"waiting on dependencies: {string.Join(", ", unresolvedDependencies)}");
        }

        if (task.Status == DomainTaskStatus.Review)
        {
            return new TaskBlockerContext(true, task.TaskId, null, "local_review_boundary", false, "waiting for review");
        }

        if (task.Status == DomainTaskStatus.ApprovalWait)
        {
            return new TaskBlockerContext(true, task.TaskId, null, "local_approval_boundary", false, "waiting for permission approval");
        }

        var taskReason = task.PlannerReview.Reason ?? task.LastRecoveryReason ?? task.LastWorkerSummary;
        if (!string.IsNullOrWhiteSpace(taskReason)
            && TryResolveLifecycleReference(taskReason, lifecycleRecords) is { } referencedLifecycle
            && !string.Equals(referencedLifecycle.TaskId, task.TaskId, StringComparison.Ordinal))
        {
            return BuildLifecycleBlocker(referencedLifecycle, task.TaskId, "external_residue", taskReason);
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            if (delegatedLifecycle is not null && delegatedLifecycle.RequiresOperatorAction)
            {
                return BuildLifecycleBlocker(delegatedLifecycle, task.TaskId, "local_residue", delegatedLifecycle.Summary);
            }

            return new TaskBlockerContext(true, task.TaskId, null, "local_task", false, taskReason ?? "blocked");
        }

        if (task.Status != DomainTaskStatus.Pending || session is null)
        {
            return TaskBlockerContext.None;
        }

        if (session.Status == RuntimeSessionStatus.ApprovalWait)
        {
            if (session.PendingPermissionRequestIds.Count == 0)
            {
                return TaskBlockerContext.None;
            }

            return new TaskBlockerContext(
                true,
                null,
                session.PendingPermissionRequestIds[0],
                "external_approval_boundary",
                false,
                $"waiting for permission approval: {string.Join(", ", session.PendingPermissionRequestIds)}");
        }

        if (session.Status == RuntimeSessionStatus.ReviewWait)
        {
            if (session.ReviewPendingTaskIds.Count == 0)
            {
                return TaskBlockerContext.None;
            }

            var reviewPendingSelf = session.ReviewPendingTaskIds.Contains(task.TaskId, StringComparer.Ordinal);
            if (reviewPendingSelf && task.Status != DomainTaskStatus.Review)
            {
                return TaskBlockerContext.None;
            }

            var boundaryTaskId = reviewPendingSelf ? task.TaskId : session.ReviewPendingTaskIds[0];
            return new TaskBlockerContext(
                true,
                boundaryTaskId,
                null,
                reviewPendingSelf ? "local_review_boundary" : "external_review_boundary",
                false,
                $"waiting for review: {boundaryTaskId}");
        }

        return TaskBlockerContext.None;
    }

    private static TaskBlockerContext BuildLifecycleBlocker(
        DelegatedRunLifecycleRecord lifecycle,
        string inspectedTaskId,
        string defaultScope,
        string? summary = null)
    {
        var external = !string.Equals(lifecycle.TaskId, inspectedTaskId, StringComparison.Ordinal);
        return new TaskBlockerContext(
            true,
            lifecycle.TaskId,
            lifecycle.LatestRecoveryEntryId ?? lifecycle.LeaseId ?? lifecycle.RunId,
            external ? defaultScope : "local_residue",
            external,
            summary ?? lifecycle.Summary);
    }

    private static DelegatedRunLifecycleRecord? TryResolveLifecycleReference(
        string reason,
        IReadOnlyList<DelegatedRunLifecycleRecord> lifecycleRecords)
    {
        foreach (Match match in TaskIdPattern.Matches(reason))
        {
            var taskId = match.Value;
            var record = lifecycleRecords.FirstOrDefault(item =>
                string.Equals(item.TaskId, taskId, StringComparison.Ordinal)
                && item.RequiresOperatorAction);
            if (record is not null)
            {
                return record;
            }
        }

        return null;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private sealed record TaskBlockerContext(
        bool BlocksDispatch,
        string? BlockerTaskId,
        string? BlockerLifecycleId,
        string BlockerScope,
        bool BlockedByExternalResidue,
        string? Summary)
    {
        public static TaskBlockerContext None { get; } = new(false, null, null, "none", false, null);
    }
}

internal sealed record DelegationDriftWarning(string Summary, string RecommendedCommand, IReadOnlyList<string> DirtyPaths);
