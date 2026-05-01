using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentQueueProjectionService
{
    private const int FirstActionableLimit = 8;

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;

    public RuntimeAgentQueueProjectionService(string repoRoot, ControlPlanePaths paths, TaskGraphService taskGraphService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeAgentQueueProjectionSurface Build()
    {
        var graph = taskGraphService.Load();
        var tasks = graph.ListTasks();
        var session = RuntimeAgentGovernanceSupport.LoadSession(paths);
        var currentTaskId = session?.CurrentTaskId;
        var currentTask = string.IsNullOrWhiteSpace(currentTaskId)
            ? null
            : tasks.FirstOrDefault(task => string.Equals(task.TaskId, currentTaskId, StringComparison.Ordinal));
        var firstActionableTasks = graph.ReadyTasks()
            .Take(FirstActionableLimit)
            .Select(ToTaskSummary)
            .ToArray();

        return new RuntimeAgentQueueProjectionSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Projection = new RuntimeAgentQueueProjection
            {
                Summary = firstActionableTasks.Length == 0
                    ? "Compact queue projection is available; no ready pending task was found."
                    : $"Compact queue projection is available with {firstActionableTasks.Length} ready pending task(s).",
                CurrentTask = BuildCurrentTaskPosture(currentTaskId, currentTask),
                Counts = BuildCounts(tasks),
                FirstActionableTasks = firstActionableTasks,
                ExpansionPointers = new RuntimeAgentQueueExpansionPointers(),
            },
        };
    }

    private static RuntimeAgentCurrentTaskPosture BuildCurrentTaskPosture(string? currentTaskId, TaskNode? currentTask)
    {
        if (string.IsNullOrWhiteSpace(currentTaskId))
        {
            return new RuntimeAgentCurrentTaskPosture();
        }

        if (currentTask is null)
        {
            return new RuntimeAgentCurrentTaskPosture
            {
                TaskId = currentTaskId,
                Source = "runtime_session_missing_task_graph_node",
                Actionability = "inspect_task_truth_before_execution",
                InspectCommand = $"inspect task {currentTaskId}",
                OverlayCommand = $"inspect runtime-agent-task-overlay {currentTaskId}",
            };
        }

        return new RuntimeAgentCurrentTaskPosture
        {
            TaskId = currentTask.TaskId,
            CardId = currentTask.CardId ?? "CARD-UNKNOWN",
            Title = currentTask.Title,
            Status = ToSnakeCase(currentTask.Status),
            Priority = currentTask.Priority,
            Actionability = ResolveActionability(currentTask),
            InspectCommand = $"inspect task {currentTask.TaskId}",
            OverlayCommand = $"inspect runtime-agent-task-overlay {currentTask.TaskId}",
        };
    }

    private static RuntimeAgentQueueStatusCounts BuildCounts(IReadOnlyList<TaskNode> tasks)
    {
        return new RuntimeAgentQueueStatusCounts
        {
            TotalCount = tasks.Count,
            SuggestedCount = Count(tasks, DomainTaskStatus.Suggested),
            PendingCount = Count(tasks, DomainTaskStatus.Pending),
            DeferredCount = Count(tasks, DomainTaskStatus.Deferred),
            RunningCount = Count(tasks, DomainTaskStatus.Running),
            TestingCount = Count(tasks, DomainTaskStatus.Testing),
            ReviewCount = Count(tasks, DomainTaskStatus.Review),
            ApprovalWaitCount = Count(tasks, DomainTaskStatus.ApprovalWait),
            BlockedCount = Count(tasks, DomainTaskStatus.Blocked),
            FailedCount = Count(tasks, DomainTaskStatus.Failed),
            CompletedCount = Count(tasks, DomainTaskStatus.Completed),
            MergedCount = Count(tasks, DomainTaskStatus.Merged),
            DiscardedCount = Count(tasks, DomainTaskStatus.Discarded),
            SupersededCount = Count(tasks, DomainTaskStatus.Superseded),
        };
    }

    private static int Count(IReadOnlyList<TaskNode> tasks, DomainTaskStatus status)
    {
        return tasks.Count(task => task.Status == status);
    }

    private static RuntimeAgentQueueTaskSummary ToTaskSummary(TaskNode task)
    {
        return new RuntimeAgentQueueTaskSummary
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? "CARD-UNKNOWN",
            Title = task.Title,
            Status = ToSnakeCase(task.Status),
            Priority = task.Priority,
            TaskType = ToSnakeCase(task.TaskType),
            InspectCommand = $"inspect task {task.TaskId}",
            OverlayCommand = $"inspect runtime-agent-task-overlay {task.TaskId}",
            RunCommand = $"task run {task.TaskId}",
        };
    }

    private static string ResolveActionability(TaskNode task)
    {
        return task.Status switch
        {
            DomainTaskStatus.Pending => "pending_worker_dispatch_when_dependencies_satisfied",
            DomainTaskStatus.Running => "worker_execution_in_progress",
            DomainTaskStatus.Testing => "validation_in_progress",
            DomainTaskStatus.Review => "planner_review_required",
            DomainTaskStatus.ApprovalWait => "operator_approval_required",
            DomainTaskStatus.Blocked => "blocked_until_dependency_or_operator_action_changes",
            DomainTaskStatus.Failed => "failure_recovery_or_review_required",
            DomainTaskStatus.Deferred => "deferred_not_default_actionable",
            DomainTaskStatus.Completed or DomainTaskStatus.Merged => "closed_history_not_default_actionable",
            _ => "not_default_actionable",
        };
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
