using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.Tests;

public sealed class TaskStatusTransitionPolicyTests
{
    [Theory]
    [InlineData(DomainTaskStatus.Pending, DomainTaskStatus.Running)]
    [InlineData(DomainTaskStatus.Running, DomainTaskStatus.Review)]
    [InlineData(DomainTaskStatus.Review, DomainTaskStatus.Completed)]
    [InlineData(DomainTaskStatus.Completed, DomainTaskStatus.Review)]
    [InlineData(DomainTaskStatus.Completed, DomainTaskStatus.Merged)]
    [InlineData(DomainTaskStatus.Blocked, DomainTaskStatus.Pending)]
    [InlineData(DomainTaskStatus.Failed, DomainTaskStatus.Completed)]
    public void CanTransition_AllowsGovernedRuntimeFlow(DomainTaskStatus from, DomainTaskStatus to)
    {
        Assert.True(TaskStatusTransitionPolicy.CanTransition(from, to));
    }

    [Theory]
    [InlineData(DomainTaskStatus.Suggested, DomainTaskStatus.Completed)]
    [InlineData(DomainTaskStatus.Completed, DomainTaskStatus.Pending)]
    [InlineData(DomainTaskStatus.Completed, DomainTaskStatus.Running)]
    [InlineData(DomainTaskStatus.Merged, DomainTaskStatus.Pending)]
    [InlineData(DomainTaskStatus.Discarded, DomainTaskStatus.Pending)]
    [InlineData(DomainTaskStatus.Superseded, DomainTaskStatus.Pending)]
    public void CanTransition_RejectsUngovernedResurrectionOrSkippedFlow(DomainTaskStatus from, DomainTaskStatus to)
    {
        Assert.False(TaskStatusTransitionPolicy.CanTransition(from, to, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void SetStatus_RejectsIllegalTransition()
    {
        var task = new TaskNode
        {
            TaskId = "T-TRANSITION-001",
            Title = "Completed task",
            Status = DomainTaskStatus.Completed,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => task.SetStatus(DomainTaskStatus.Pending));

        Assert.Contains("Completed -> Pending", exception.Message, StringComparison.Ordinal);
        Assert.Equal(DomainTaskStatus.Completed, task.Status);
    }

    [Fact]
    public void Status_IsInitOnlySoRuntimeMutationMustUseSetStatus()
    {
        var property = typeof(TaskNode).GetProperty(nameof(TaskNode.Status));
        var setter = property?.SetMethod;

        Assert.NotNull(setter);
        Assert.Contains(
            setter.ReturnParameter.GetRequiredCustomModifiers(),
            modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }
}
