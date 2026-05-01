namespace Carves.Runtime.Domain.Tasks;

public sealed class TaskTypePolicy
{
    public static TaskTypePolicy Default { get; } = new();

    public bool AllowSchedulerDispatch(TaskType taskType)
    {
        return taskType == TaskType.Execution;
    }

    public bool AllowWorkerExecution(TaskType taskType)
    {
        return taskType == TaskType.Execution;
    }

    public bool RequireBuildValidation(TaskType taskType)
    {
        return taskType == TaskType.Execution;
    }

    public bool RequireTestValidation(TaskType taskType)
    {
        return taskType == TaskType.Execution;
    }

    public bool RequireReviewBoundary(TaskType taskType)
    {
        return taskType == TaskType.Execution;
    }

    public bool AllowPlannerGeneration(TaskType taskType)
    {
        return taskType is TaskType.Execution or TaskType.Planning or TaskType.Meta;
    }

    public string DescribeDispatchEligibility(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.Execution => "dispatchable: execution tasks may be leased by the worker pool",
            TaskType.Review => "governed: review tasks remain behind the review boundary",
            TaskType.Planning => "governed: planning tasks require planner or human follow-up rather than worker execution",
            TaskType.Meta => "governed: meta tasks stay visible but are not dispatched to workers",
            _ => "governed: task type is not eligible for worker dispatch",
        };
    }

    public string DescribeSafetyProfile(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.Execution => "execution validation: build, test, patch, architecture, and retry policy",
            TaskType.Review => "review validation: integrity and control-plane checks only",
            TaskType.Planning => "planning validation: schema and control-plane integrity checks only",
            TaskType.Meta => "meta validation: system integrity checks only",
            _ => "governed validation: integrity checks only",
        };
    }
}
