using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Safety;

public sealed class SafetyTaskClassifier
{
    private readonly TaskTypePolicy taskTypePolicy;

    public SafetyTaskClassifier(TaskTypePolicy? taskTypePolicy = null)
    {
        this.taskTypePolicy = taskTypePolicy ?? TaskTypePolicy.Default;
    }

    public SafetyValidationMode Classify(TaskNode task)
    {
        return task.TaskType switch
        {
            TaskType.Execution => SafetyValidationMode.Execution,
            TaskType.Review => SafetyValidationMode.Review,
            TaskType.Planning => SafetyValidationMode.Planning,
            TaskType.Meta => SafetyValidationMode.Meta,
            _ => SafetyValidationMode.Meta,
        };
    }

    public bool AllowsWorkerExecution(TaskNode task)
    {
        return taskTypePolicy.AllowWorkerExecution(task.TaskType);
    }

    public bool RequiresBuildValidation(TaskNode task)
    {
        return taskTypePolicy.RequireBuildValidation(task.TaskType);
    }

    public bool RequiresTestValidation(TaskNode task)
    {
        return taskTypePolicy.RequireTestValidation(task.TaskType);
    }

    public bool ShouldRunValidator(SafetyValidationMode mode, ISafetyValidator validator)
    {
        return mode switch
        {
            SafetyValidationMode.Execution => true,
            SafetyValidationMode.Review => validator is TaskIntegritySafetyValidator
                or FileAccessSafetyValidator
                or TaskScopeSafetyValidator
                or ManagedControlPlaneSafetyValidator
                or PatchSizeSafetyValidator,
            SafetyValidationMode.Planning => validator is TaskIntegritySafetyValidator
                or FileAccessSafetyValidator
                or ManagedControlPlaneSafetyValidator,
            SafetyValidationMode.Meta => validator is TaskIntegritySafetyValidator
                or FileAccessSafetyValidator
                or ManagedControlPlaneSafetyValidator,
            _ => false,
        };
    }
}
