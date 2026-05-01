using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.Tests;

public sealed class TaskGraphTests
{
    [Fact]
    public void SelectNextReadyTask_ChoosesReadyTaskByPriority()
    {
        var graph = new TaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-2",
                Title = "Lower priority ready task",
                Status = DomainTaskStatus.Pending,
                Priority = "P2",
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-1",
                Title = "Higher priority ready task",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Acceptance = ["accepted"],
            },
        ]);

        var nextTask = graph.SelectNextReadyTask();

        Assert.NotNull(nextTask);
        Assert.Equal("T-1", nextTask!.TaskId);
    }

    [Fact]
    public void ReadyTasks_ExcludePendingTasksWithIncompleteDependencies()
    {
        var graph = new TaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-1",
                Title = "Ready pending task",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Dependencies = ["T-2"],
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-2",
                Title = "Completed dependency",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-3",
                Title = "Blocked pending task",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Dependencies = ["T-4"],
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-4",
                Title = "Incomplete dependency",
                Status = DomainTaskStatus.Pending,
                Priority = "P2",
                Acceptance = ["accepted"],
            },
        ]);

        var readyTasks = graph.ReadyTasks();

        Assert.Contains(readyTasks, task => task.TaskId == "T-1");
        Assert.DoesNotContain(readyTasks, task => task.TaskId == "T-3");
    }

    [Fact]
    public void ReadyTasks_ExcludeDeferredTasks()
    {
        var graph = new TaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-1",
                Title = "Deferred task",
                Status = DomainTaskStatus.Deferred,
                Priority = "P1",
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-2",
                Title = "Pending task",
                Status = DomainTaskStatus.Pending,
                Priority = "P2",
                Acceptance = ["accepted"],
            },
        ]);

        var readyTasks = graph.ReadyTasks();

        Assert.DoesNotContain(readyTasks, task => task.TaskId == "T-1");
        Assert.Contains(readyTasks, task => task.TaskId == "T-2");
    }
}
