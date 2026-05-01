using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.TaskGraph;

public sealed class TaskConflictDetector
{
    public TaskConflict? Detect(
        TaskNode candidate,
        IEnumerable<TaskNode> selectedTasks,
        IEnumerable<TaskNode> runningTasks,
        IEnumerable<TaskNode> reviewTasks)
    {
        foreach (var task in runningTasks)
        {
            if (Conflicts(candidate, task))
            {
                return new TaskConflict(TaskConflictKind.AlreadyRunning, task, DescribeConflict(candidate, task, "running"));
            }
        }

        foreach (var task in reviewTasks)
        {
            if (Conflicts(candidate, task))
            {
                return new TaskConflict(TaskConflictKind.ReviewPending, task, DescribeConflict(candidate, task, "review-pending"));
            }
        }

        foreach (var task in selectedTasks)
        {
            if (Conflicts(candidate, task))
            {
                return new TaskConflict(TaskConflictKind.ScopeOverlap, task, DescribeConflict(candidate, task, "concurrently selected"));
            }
        }

        return null;
    }

    private static bool Conflicts(TaskNode left, TaskNode right)
    {
        var leftScopes = TaskScopeAdmissionGate.NormalizeScope(left.Scope);
        var rightScopes = TaskScopeAdmissionGate.NormalizeScope(right.Scope);
        if (leftScopes.Count == 0 || rightScopes.Count == 0)
        {
            return true;
        }

        return leftScopes.Any(leftScope => rightScopes.Any(rightScope => Overlaps(leftScope, rightScope)));
    }

    private static string DescribeConflict(TaskNode candidate, TaskNode existingTask, string existingTaskPosture)
    {
        if (TaskScopeAdmissionGate.NormalizeScope(candidate.Scope).Count == 0
            || TaskScopeAdmissionGate.NormalizeScope(existingTask.Scope).Count == 0)
        {
            return $"Scope is missing or underspecified; cannot safely run concurrently with {existingTaskPosture} task {existingTask.TaskId}.";
        }

        return $"Scope overlaps with {existingTaskPosture} task {existingTask.TaskId}.";
    }

    private static bool Overlaps(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
               left.StartsWith($"{right}/", StringComparison.OrdinalIgnoreCase) ||
               right.StartsWith($"{left}/", StringComparison.OrdinalIgnoreCase);
    }
}
