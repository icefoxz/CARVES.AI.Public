using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class TaskTypePolicyTests
{
    [Fact]
    public void DefaultPolicy_UsesExecutionAsTheOnlyDispatchableWorkerTaskType()
    {
        var policy = TaskTypePolicy.Default;

        Assert.True(policy.AllowSchedulerDispatch(TaskType.Execution));
        Assert.True(policy.AllowWorkerExecution(TaskType.Execution));
        Assert.True(policy.RequireBuildValidation(TaskType.Execution));
        Assert.True(policy.RequireReviewBoundary(TaskType.Execution));

        Assert.False(policy.AllowSchedulerDispatch(TaskType.Review));
        Assert.False(policy.AllowSchedulerDispatch(TaskType.Planning));
        Assert.False(policy.AllowSchedulerDispatch(TaskType.Meta));
        Assert.False(policy.AllowWorkerExecution(TaskType.Review));
        Assert.False(policy.AllowWorkerExecution(TaskType.Planning));
        Assert.False(policy.AllowWorkerExecution(TaskType.Meta));
        Assert.False(policy.RequireBuildValidation(TaskType.Planning));
        Assert.False(policy.RequireTestValidation(TaskType.Meta));
    }
}
