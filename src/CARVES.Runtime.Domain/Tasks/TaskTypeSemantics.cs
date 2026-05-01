namespace Carves.Runtime.Domain.Tasks;

public static class TaskTypeSemantics
{
    private static TaskTypePolicy Policy => TaskTypePolicy.Default;

    public static bool CanDispatchToWorkerPool(this TaskType taskType)
    {
        return Policy.AllowSchedulerDispatch(taskType);
    }

    public static bool CanExecuteInWorker(this TaskType taskType)
    {
        return Policy.AllowWorkerExecution(taskType);
    }

    public static bool RequiresBuildValidation(this TaskType taskType)
    {
        return Policy.RequireBuildValidation(taskType);
    }

    public static bool RequiresTestValidation(this TaskType taskType)
    {
        return Policy.RequireTestValidation(taskType);
    }

    public static bool RequiresReviewBoundary(this TaskType taskType)
    {
        return Policy.RequireReviewBoundary(taskType);
    }

    public static bool CanBePlannerGenerated(this TaskType taskType)
    {
        return Policy.AllowPlannerGeneration(taskType);
    }

    public static string DescribeDispatchEligibility(this TaskType taskType)
    {
        return Policy.DescribeDispatchEligibility(taskType);
    }

    public static string DescribeSafetyProfile(this TaskType taskType)
    {
        return Policy.DescribeSafetyProfile(taskType);
    }
}
