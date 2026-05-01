using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;

namespace Carves.Runtime.Application.Tests;

public sealed class ParallelSchedulingTests
{
    [Fact]
    public void Decide_SelectsTwoTasksAndExplainsConflictAndConcurrencyBlocks()
    {
        var graph = new DomainTaskGraph(
        [
            CreatePendingTask("T-001", "src/Area/A"),
            CreatePendingTask("T-002", "src/Area/A/Child"),
            CreatePendingTask("T-003", "src/Area/B"),
            CreatePendingTask("T-004", "src/Area/C"),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 4, Array.Empty<string>()));

        Assert.True(decision.ShouldDispatch);
        Assert.Equal(["T-001", "T-003"], decision.Tasks.Select(task => task.TaskId));
        Assert.Contains(decision.BlockedTasks, block => block.TaskId == "T-002" && block.Kind == TaskScheduleBlockKind.Conflict);
        Assert.Contains(decision.BlockedTasks, block => block.TaskId == "T-004" && block.Kind == TaskScheduleBlockKind.ConcurrencyCap);
    }

    [Fact]
    public void Decide_BlocksAllDispatchWhileReviewBoundaryIsActive()
    {
        var graph = new DomainTaskGraph(
        [
            CreateReviewTask("T-REVIEW", "src/Boundaries/A"),
            CreatePendingTask("T-CONFLICT", "src/Boundaries/A/Child"),
            CreatePendingTask("T-UNRELATED", "src/Boundaries/B"),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);
        session.MarkReviewWait("T-REVIEW", "Waiting on review.");

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 4, Array.Empty<string>()));

        Assert.False(decision.ShouldDispatch);
        Assert.Equal(TaskScheduleIdleReason.ReviewBoundary, decision.IdleReason);
        Assert.Empty(decision.Tasks);
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-CONFLICT" &&
                     block.Kind == TaskScheduleBlockKind.ReviewBoundary &&
                     block.Reason.Contains("T-REVIEW", StringComparison.Ordinal));
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-UNRELATED" &&
                     block.Kind == TaskScheduleBlockKind.ReviewBoundary &&
                     block.Reason.Contains("T-REVIEW", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectNext_ReturnsNullWhileReviewBoundaryIsActive()
    {
        var graph = new DomainTaskGraph(
        [
            CreateReviewTask("T-REVIEW", "src/Boundaries/A"),
            CreatePendingTask("T-PENDING", "src/Boundaries/B"),
        ]);
        var scheduler = new ApplicationTaskScheduler();

        var next = scheduler.SelectNext(graph);

        Assert.Null(next);
    }

    [Fact]
    public void SelectNext_ReturnsNullForMissingScopeExecutionTask()
    {
        var graph = new DomainTaskGraph(
        [
            CreatePendingTask("T-MISSING-SCOPE", ""),
        ]);
        var scheduler = new ApplicationTaskScheduler();

        var next = scheduler.SelectNext(graph);

        Assert.Null(next);
    }

    [Fact]
    public void Decide_BlocksPlanningAndMetaTasksFromWorkerDispatch()
    {
        var graph = new DomainTaskGraph(
        [
            CreateTask("T-PLAN", TaskType.Planning, "docs/runtime"),
            CreateTask("T-META", TaskType.Meta, ".ai/tasks"),
            CreateTask("T-EXEC", TaskType.Execution, "src/Runtime"),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 4, Array.Empty<string>()));

        Assert.True(decision.ShouldDispatch);
        Assert.Equal(["T-EXEC"], decision.Tasks.Select(task => task.TaskId));
        Assert.Contains(decision.BlockedTasks, block => block.TaskId == "T-PLAN" && block.Kind == TaskScheduleBlockKind.TaskType);
        Assert.Contains(decision.BlockedTasks, block => block.TaskId == "T-META" && block.Kind == TaskScheduleBlockKind.TaskType);
    }

    [Fact]
    public void Decide_BlocksExecutionTaskWithoutAcceptanceContract()
    {
        var graph = new DomainTaskGraph(
        [
            CreateTask("T-MISSING-CONTRACT", TaskType.Execution, "src/MissingContract", withAcceptanceContract: false),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 2, Array.Empty<string>()));

        Assert.False(decision.ShouldDispatch);
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-MISSING-CONTRACT"
                     && block.Kind == TaskScheduleBlockKind.Governance
                     && block.Reason.Contains("missing an acceptance contract", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_BlocksMissingScopeBeforeConcurrencySelection()
    {
        var graph = new DomainTaskGraph(
        [
            CreatePendingTask("T-001-MISSING-SCOPE", ""),
            CreatePendingTask("T-002-SCOPED", "src/Area/B"),
            CreatePendingTask("T-003-ANOTHER-SCOPED", "src/Area/C"),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 4, Array.Empty<string>()));

        Assert.True(decision.ShouldDispatch);
        Assert.Equal(["T-002-SCOPED", "T-003-ANOTHER-SCOPED"], decision.Tasks.Select(task => task.TaskId));
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-001-MISSING-SCOPE"
                     && block.Kind == TaskScheduleBlockKind.Governance
                     && block.Reason.Contains("scope is missing or underspecified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Decide_BlocksDotScopeBeforeDispatch()
    {
        var graph = new DomainTaskGraph(
        [
            CreatePendingTask("T-DOT-SCOPE", "."),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(0, 4, Array.Empty<string>()));

        Assert.False(decision.ShouldDispatch);
        Assert.Equal(TaskScheduleIdleReason.NoReadyExecutionTask, decision.IdleReason);
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-DOT-SCOPE"
                     && block.Kind == TaskScheduleBlockKind.Governance
                     && block.Reason.Contains("scope is missing or underspecified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Decide_BlocksPendingTaskWhenRunningTaskHasMissingScope()
    {
        var graph = new DomainTaskGraph(
        [
            CreateTask("T-001-RUNNING-MISSING-SCOPE", TaskType.Execution, "", DomainTaskStatus.Running),
            CreatePendingTask("T-002-SCOPED", "src/Area/B"),
        ]);
        var scheduler = new ApplicationTaskScheduler();
        var session = RuntimeSessionState.Start("repo", dryRun: false);

        var decision = scheduler.Decide(graph, session, new WorkerPoolSnapshot(1, 4, Array.Empty<string>()));

        Assert.False(decision.ShouldDispatch);
        Assert.Equal(TaskScheduleIdleReason.AllCandidatesBlocked, decision.IdleReason);
        Assert.Contains(
            decision.BlockedTasks,
            block => block.TaskId == "T-002-SCOPED"
                     && block.Kind == TaskScheduleBlockKind.Conflict
                     && block.Reason.Contains("running task T-001-RUNNING-MISSING-SCOPE", StringComparison.Ordinal));
    }

    private static TaskNode CreatePendingTask(string taskId, string scope)
    {
        return CreateTask(taskId, TaskType.Execution, scope, DomainTaskStatus.Pending);
    }

    private static TaskNode CreateReviewTask(string taskId, string scope)
    {
        return CreateTask(taskId, TaskType.Execution, scope, DomainTaskStatus.Review);
    }

    private static TaskNode CreateTask(string taskId, TaskType taskType, string scope, DomainTaskStatus status = DomainTaskStatus.Pending, bool withAcceptanceContract = true)
    {
        return new TaskNode
        {
            TaskId = taskId,
            Title = taskId,
            Status = status,
            TaskType = taskType,
            Priority = "P1",
            Scope = [scope],
            Acceptance = ["scheduled"],
            AcceptanceContract = withAcceptanceContract
                ? new AcceptanceContract
                {
                    ContractId = $"AC-{taskId}",
                    Title = $"Acceptance contract for {taskId}",
                    Status = AcceptanceContractLifecycleStatus.Compiled,
                    Traceability = new AcceptanceContractTraceability
                    {
                        SourceTaskId = taskId,
                    },
                }
                : null,
        };
    }
}
