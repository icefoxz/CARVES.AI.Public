using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class DispatchProjectionServiceTests
{
    [Fact]
    public void Build_ReturnsDispatchableWhenReadyExecutionTaskExists()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-201-001",
                Title = "dispatchable",
                Description = "ready task",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                TaskType = TaskType.Execution,
                AcceptanceContract = CreateAcceptanceContract("T-CARD-201-001"),
            },
        ]);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, null, maxWorkers: 2);

        Assert.Equal("dispatchable", projection.State);
        Assert.Equal("T-CARD-201-001", projection.NextTaskId);
        Assert.Equal("READY_TASK_AVAILABLE", projection.IdleReason);
    }

    [Fact]
    public void Build_ReturnsDispatchBlockedForReviewBoundary()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-202-001",
                Title = "review",
                Description = "review task",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Review,
                TaskType = TaskType.Execution,
            },
        ]);
        var session = RuntimeSessionState.Start("repo", dryRun: false);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, session, maxWorkers: 1);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("WAITING_APPROVAL", projection.IdleReason);
    }

    [Fact]
    public void Build_ClassifiesBlockedByDependencyWhenPendingTaskWaitsOnUnresolvedDependency()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-235-DEP-001",
                Title = "blocked",
                Description = "blocked by dependency",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                TaskType = TaskType.Execution,
                Dependencies = ["T-CARD-235-DEP-000"],
            },
        ]);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, RuntimeSessionState.Start("repo", dryRun: false), maxWorkers: 1);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("BLOCKED_BY_DEPENDENCY", projection.IdleReason);
    }

    [Fact]
    public void Build_ClassifiesFailedNeedsReplanWhenPlannerMetadataMarksTaskForReplan()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-235-REPLAN-001",
                Title = "replan",
                Description = "needs replan",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Blocked,
                TaskType = TaskType.Execution,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["planner_replan_required"] = "true",
                },
            },
        ]);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, RuntimeSessionState.Start("repo", dryRun: false), maxWorkers: 1);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("FAILED_NEEDS_REPLAN", projection.IdleReason);
    }

    [Fact]
    public void Build_ClassifiesWorkerUnavailableWhenReadyTaskExistsButNoWorkerSlotsAreConfigured()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-237-001",
                Title = "ready",
                Description = "ready task",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                TaskType = TaskType.Execution,
                AcceptanceContract = CreateAcceptanceContract("T-CARD-237-001"),
            },
        ]);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, RuntimeSessionState.Start("repo", dryRun: false), maxWorkers: 0);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("WORKER_UNAVAILABLE", projection.IdleReason);
    }

    [Fact]
    public void Build_ClassifiesMissingAcceptanceContractWhenReadyExecutionTaskHasNoContract()
    {
        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-CARD-237-GATE-001",
                Title = "gate",
                Description = "missing acceptance contract",
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                TaskType = TaskType.Execution,
            },
        ]);
        var service = new DispatchProjectionService();

        var projection = service.Build(graph, RuntimeSessionState.Start("repo", dryRun: false), maxWorkers: 1);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("MISSING_ACCEPTANCE_CONTRACT", projection.IdleReason);
        Assert.Equal(0, projection.ReadyTaskCount);
        Assert.Equal("T-CARD-237-GATE-001", projection.FirstBlockedTaskId);
        Assert.Equal("acceptance_contract_projected", projection.FirstBlockingCheckId);
        Assert.Equal("inspect task T-CARD-237-GATE-001", projection.FirstBlockingCheckRequiredCommand);
        Assert.Equal("project a minimum acceptance contract onto task truth before dispatch", projection.RecommendedNextAction);
    }

    [Fact]
    public void DescribeIdleReason_UsesDispatchableTerminology()
    {
        var service = new DispatchProjectionService();

        Assert.Equal("dispatchable task available", service.DescribeIdleReason("READY_TASK_AVAILABLE"));
        Assert.Equal("no dispatchable task", service.DescribeIdleReason("NO_READY_TASK"));
        Assert.Equal(
            "all dependency-ready candidates blocked by governance",
            service.DescribeIdleReason(nameof(Carves.Runtime.Application.TaskGraph.TaskScheduleIdleReason.AllCandidatesBlocked)));
    }

    private static AcceptanceContract CreateAcceptanceContract(string taskId)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Acceptance contract for {taskId}",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            Traceability = new AcceptanceContractTraceability
            {
                SourceTaskId = taskId,
            },
        };
    }
}
