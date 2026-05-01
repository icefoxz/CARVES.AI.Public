using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Workers;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.TaskGraph;

public sealed class TaskGraphService
{
    private readonly ITaskGraphRepository repository;
    private readonly TaskScheduler scheduler;
    private readonly IControlPlaneLockService lockService;

    public TaskGraphService(ITaskGraphRepository repository, TaskScheduler scheduler, IControlPlaneLockService? lockService = null)
    {
        this.repository = repository;
        this.scheduler = scheduler;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public DomainTaskGraph Load()
    {
        return repository.Load();
    }

    public TaskNode GetTask(string taskId)
    {
        var graph = repository.Load();
        if (!graph.Tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        return task;
    }

    public TaskNode? NextReadyTask()
    {
        return scheduler.SelectNext(repository.Load());
    }

    public TaskScheduleDecision DecideNext(RuntimeSessionState session, WorkerPoolSnapshot workerPool)
    {
        return scheduler.Decide(repository.Load(), session, workerPool);
    }

    public DomainTaskGraph AddTasks(IEnumerable<TaskNode> tasks)
    {
        using var _ = lockService.Acquire("task-graph");
        var changedTasks = tasks.ToArray();
        EnsureReplaceTransitions(changedTasks, "task-graph-add-tasks");
        repository.UpsertRange(changedTasks);
        return repository.Load();
    }

    public DomainTaskGraph Normalize()
    {
        using var _ = lockService.Acquire("task-graph");
        var graph = repository.Load();
        repository.Save(graph);
        return repository.Load();
    }

    public TaskNode MarkStatus(string taskId, DomainTaskStatus status)
    {
        using var _ = lockService.Acquire("task-graph");
        var graph = repository.Load();
        var task = graph.Tasks[taskId];
        task.SetStatus(status);
        repository.Save(graph);
        return task;
    }

    public TaskNode ReplaceTask(TaskNode task)
    {
        using var _ = lockService.Acquire("task-graph");
        EnsureReplaceTransitions([task], "task-graph-replace-task");
        repository.Upsert(task);
        return task;
    }

    public IReadOnlyList<string> SupersedeCardTasks(string cardId, string reason)
    {
        using var _ = lockService.Acquire("task-graph");
        var graph = repository.Load();
        var supersededTaskIds = new List<string>();
        foreach (var task in graph.Tasks.Values.Where(task => string.Equals(task.CardId, cardId, StringComparison.Ordinal)))
        {
            if (task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded)
            {
                continue;
            }

            task.SetStatus(DomainTaskStatus.Superseded);
            task.ClearRetryBackoff();
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = reason,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = Array.Empty<string>(),
            });
            supersededTaskIds.Add(task.TaskId);
        }

        if (supersededTaskIds.Count > 0)
        {
            repository.Save(graph);
        }

        return supersededTaskIds;
    }

    private void EnsureReplaceTransitions(IReadOnlyCollection<TaskNode> tasks, string operation)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        var graph = repository.Load();
        foreach (var task in tasks)
        {
            if (!graph.Tasks.TryGetValue(task.TaskId, out var existingTask))
            {
                continue;
            }

            TaskStatusTransitionPolicy.EnsureCanTransition(task.TaskId, existingTask.Status, task.Status, operation);
        }
    }
}
