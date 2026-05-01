using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class TaskGraphServiceTransitionPolicyTests
{
    [Fact]
    public void ReplaceTask_RejectsCloneBasedIllegalStatusTransition()
    {
        var original = CreateTask("T-REPLACE-001", DomainTaskStatus.Completed);
        var service = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([original])),
            new Application.TaskGraph.TaskScheduler());
        var clone = CreateTask("T-REPLACE-001", DomainTaskStatus.Pending);

        var exception = Assert.Throws<InvalidOperationException>(() => service.ReplaceTask(clone));

        Assert.Contains("Completed -> Pending", exception.Message, StringComparison.Ordinal);
        Assert.Equal(DomainTaskStatus.Completed, service.GetTask("T-REPLACE-001").Status);
    }

    [Fact]
    public void AddTasks_RejectsExistingTaskIllegalStatusTransition()
    {
        var original = CreateTask("T-ADD-001", DomainTaskStatus.Suggested);
        var service = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([original])),
            new Application.TaskGraph.TaskScheduler());
        var replacement = CreateTask("T-ADD-001", DomainTaskStatus.Completed);

        var exception = Assert.Throws<InvalidOperationException>(() => service.AddTasks([replacement]));

        Assert.Contains("Suggested -> Completed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(DomainTaskStatus.Suggested, service.GetTask("T-ADD-001").Status);
    }

    [Fact]
    public void ReplaceTask_AllowsMetadataOnlyUpdateWithoutStatusChange()
    {
        var original = CreateTask("T-REPLACE-002", DomainTaskStatus.Completed);
        var service = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([original])),
            new Application.TaskGraph.TaskScheduler());
        var replacement = new TaskNode
        {
            TaskId = original.TaskId,
            Title = original.Title,
            Status = original.Status,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidence"] = "retained",
            },
        };

        service.ReplaceTask(replacement);

        var stored = service.GetTask("T-REPLACE-002");
        Assert.Equal(DomainTaskStatus.Completed, stored.Status);
        Assert.Equal("retained", stored.Metadata["evidence"]);
    }

    private static TaskNode CreateTask(string taskId, DomainTaskStatus status)
    {
        return new TaskNode
        {
            TaskId = taskId,
            Title = taskId,
            Status = status,
            Acceptance = ["accepted"],
        };
    }
}
