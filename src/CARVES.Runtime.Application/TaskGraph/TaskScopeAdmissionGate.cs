using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.TaskGraph;

public static class TaskScopeAdmissionGate
{
    public static TaskScheduleBlock? Evaluate(TaskNode task)
    {
        return HasDispatchableScope(task)
            ? null
            : new TaskScheduleBlock(
                task.TaskId,
                TaskScheduleBlockKind.Governance,
                "Task scope is missing or underspecified; worker dispatch requires at least one concrete scope path before automation can run.");
    }

    public static bool HasDispatchableScope(TaskNode task)
    {
        return NormalizeScope(task.Scope).Count > 0;
    }

    internal static IReadOnlyList<string> NormalizeScope(IReadOnlyList<string> scope)
    {
        return scope
            .Select(value => value.Trim().Trim('`').Replace('\\', '/'))
            .Select(value => value.TrimStart('.', '/'))
            .Select(value => value.TrimEnd('/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
