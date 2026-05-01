using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.Planning;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.TaskGraph;

public sealed class TaskScheduler
{
    private readonly TaskConcurrencyPolicy concurrencyPolicy;
    private readonly TaskConflictDetector conflictDetector;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly ModeExecutionEntryGateService modeExecutionEntryGateService;

    public TaskScheduler(
        TaskConcurrencyPolicy? concurrencyPolicy = null,
        TaskConflictDetector? conflictDetector = null,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.concurrencyPolicy = concurrencyPolicy ?? new TaskConcurrencyPolicy();
        this.conflictDetector = conflictDetector ?? new TaskConflictDetector();
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.modeExecutionEntryGateService = new ModeExecutionEntryGateService(this.formalPlanningExecutionGateService);
    }

    public TaskNode? SelectNext(DomainTaskGraph graph)
    {
        if (graph.ByStatus(DomainTaskStatus.Review).Count > 0)
        {
            return null;
        }

        return graph.ReadyTasks().FirstOrDefault(task =>
            task.CanDispatchToWorkerPool
            && TaskScopeAdmissionGate.HasDispatchableScope(task)
            && !modeExecutionEntryGateService.Evaluate(task).BlocksExecution);
    }

    public TaskScheduleDecision Decide(DomainTaskGraph graph, RuntimeSessionState session, WorkerPoolSnapshot workerPool)
    {
        if (session.Status == RuntimeSessionStatus.Paused)
        {
            return TaskScheduleDecision.Idle("Session is paused.", workerPool.ActiveWorkers, workerPool.MaxWorkers, idleReason: TaskScheduleIdleReason.SessionPaused);
        }

        if (session.Status == RuntimeSessionStatus.Stopped)
        {
            return TaskScheduleDecision.Idle("Session is stopped.", workerPool.ActiveWorkers, workerPool.MaxWorkers, idleReason: TaskScheduleIdleReason.SessionStopped);
        }

        if (!workerPool.HasCapacity)
        {
            return TaskScheduleDecision.Idle(
                $"Worker pool is at capacity ({workerPool.ActiveWorkers}/{workerPool.MaxWorkers}).",
                workerPool.ActiveWorkers,
                workerPool.MaxWorkers,
                idleReason: TaskScheduleIdleReason.WorkerPoolAtCapacity);
        }

        var readyTasks = graph.ReadyTasks();
        var reviewTasks = graph.ByStatus(DomainTaskStatus.Review);
        if (reviewTasks.Count > 0)
        {
            return TaskScheduleDecision.Idle(
                $"Review boundary is active: {reviewTasks.Count} task(s) remain pending review; worker dispatch is held until the review boundary is settled.",
                workerPool.ActiveWorkers,
                workerPool.MaxWorkers,
                readyTasks
                    .Select(task => new TaskScheduleBlock(
                        task.TaskId,
                        TaskScheduleBlockKind.ReviewBoundary,
                        $"Review boundary is active because {DescribeReviewTasks(reviewTasks)} remain pending review."))
                    .ToArray(),
                TaskScheduleIdleReason.ReviewBoundary);
        }

        var blockedTasks = new List<TaskScheduleBlock>();
        var executableReadyTasks = new List<TaskNode>();

        foreach (var task in readyTasks)
        {
            if (!task.CanDispatchToWorkerPool)
            {
                blockedTasks.Add(new TaskScheduleBlock(task.TaskId, TaskScheduleBlockKind.TaskType, $"{task.TaskType.DescribeDispatchEligibility()}."));
                continue;
            }

            var scopeAdmissionBlock = TaskScopeAdmissionGate.Evaluate(task);
            if (scopeAdmissionBlock is not null)
            {
                blockedTasks.Add(scopeAdmissionBlock);
                continue;
            }

            var modeExecutionEntryGate = modeExecutionEntryGateService.Evaluate(task);
            if (modeExecutionEntryGate.BlocksExecution)
            {
                blockedTasks.Add(new TaskScheduleBlock(task.TaskId, TaskScheduleBlockKind.Governance, modeExecutionEntryGate.Summary));
                continue;
            }

            executableReadyTasks.Add(task);
        }

        if (executableReadyTasks.Count == 0)
        {
            if (reviewTasks.Count > 0)
            {
                return TaskScheduleDecision.Idle(
                    $"No dispatchable execution task. {reviewTasks.Count} task(s) remain pending review.",
                    workerPool.ActiveWorkers,
                    workerPool.MaxWorkers,
                    blockedTasks,
                    TaskScheduleIdleReason.ReviewBoundary);
            }

            var idleReason = blockedTasks.Count == 0
                ? "No dispatchable execution task."
                : $"No dispatchable execution task. {DescribeGovernedBlocks(blockedTasks)}.";
            return TaskScheduleDecision.Idle(
                idleReason,
                workerPool.ActiveWorkers,
                workerPool.MaxWorkers,
                blockedTasks,
                TaskScheduleIdleReason.NoReadyExecutionTask);
        }

        var dispatchCapacity = concurrencyPolicy.ResolveDispatchCapacity(workerPool);
        if (dispatchCapacity == 0)
        {
            blockedTasks.AddRange(executableReadyTasks
                .Select(task => new TaskScheduleBlock(task.TaskId, TaskScheduleBlockKind.ConcurrencyCap, "Concurrency cap reached for this session tick."))
                .ToArray());
            return TaskScheduleDecision.Idle(
                "No dispatch capacity remains.",
                workerPool.ActiveWorkers,
                workerPool.MaxWorkers,
                blockedTasks,
                TaskScheduleIdleReason.WorkerPoolAtCapacity);
        }

        var selected = new List<TaskNode>();
        var runningTasks = graph.ByStatus(DomainTaskStatus.Running);

        foreach (var task in executableReadyTasks)
        {
            if (selected.Count >= dispatchCapacity)
            {
                blockedTasks.Add(new TaskScheduleBlock(task.TaskId, TaskScheduleBlockKind.ConcurrencyCap, "Concurrency cap reached for this session tick."));
                continue;
            }

            var conflict = conflictDetector.Detect(task, selected, runningTasks, reviewTasks);
            if (conflict is not null)
            {
                blockedTasks.Add(new TaskScheduleBlock(task.TaskId, TaskScheduleBlockKind.Conflict, conflict.Reason));
                continue;
            }

            selected.Add(task);
        }

        if (selected.Count == 0)
        {
            return TaskScheduleDecision.Idle(
                blockedTasks.FirstOrDefault()?.Reason ?? "No schedulable task remained after concurrency checks.",
                workerPool.ActiveWorkers,
                workerPool.MaxWorkers,
                blockedTasks,
                TaskScheduleIdleReason.AllCandidatesBlocked);
        }

        var reason = selected.Count == 1
            ? $"Dispatched {selected[0].TaskId} from ready queue."
            : $"Dispatched {selected.Count} tasks from ready queue: {string.Join(", ", selected.Select(task => task.TaskId))}.";
        return TaskScheduleDecision.Dispatch(selected, reason, workerPool.ActiveWorkers, workerPool.MaxWorkers, blockedTasks);
    }

    private static string DescribeGovernedBlocks(IReadOnlyList<TaskScheduleBlock> blockedTasks)
    {
        var fragments = new List<string>();
        var governanceCount = blockedTasks.Count(block => block.Kind == TaskScheduleBlockKind.Governance);
        if (governanceCount > 0)
        {
            fragments.Add($"{governanceCount} task(s) are blocked by governance");
        }

        var taskTypeCount = blockedTasks.Count(block => block.Kind == TaskScheduleBlockKind.TaskType);
        if (taskTypeCount > 0)
        {
            fragments.Add($"{taskTypeCount} task(s) are governed by non-execution task types");
        }

        return fragments.Count == 0
            ? $"{blockedTasks.Count} task(s) are blocked"
            : string.Join("; ", fragments);
    }

    private static string DescribeReviewTasks(IReadOnlyList<TaskNode> reviewTasks)
    {
        var ids = reviewTasks
            .Select(task => task.TaskId)
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Take(3)
            .ToArray();
        if (ids.Length == 0)
        {
            return "review task(s)";
        }

        var suffix = reviewTasks.Count > ids.Length ? ", ..." : string.Empty;
        return string.Join(", ", ids) + suffix;
    }
}
