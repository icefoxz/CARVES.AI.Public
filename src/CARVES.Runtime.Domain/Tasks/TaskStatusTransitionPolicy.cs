namespace Carves.Runtime.Domain.Tasks;

public static class TaskStatusTransitionPolicy
{
    public static bool CanTransition(TaskStatus from, TaskStatus to)
    {
        return CanTransition(from, to, out _);
    }

    public static bool CanTransition(TaskStatus from, TaskStatus to, out string? reason)
    {
        reason = Validate(from, to);
        return reason is null;
    }

    public static void EnsureCanTransition(string taskId, TaskStatus from, TaskStatus to, string operation)
    {
        if (CanTransition(from, to, out var reason))
        {
            return;
        }

        var normalizedTaskId = string.IsNullOrWhiteSpace(taskId) ? "(unknown)" : taskId;
        throw new InvalidOperationException(
            $"Illegal task status transition for '{normalizedTaskId}' during {operation}: {from} -> {to}. {reason}");
    }

    public static bool IsFinalized(TaskStatus status)
    {
        return status is TaskStatus.Completed
            or TaskStatus.Merged
            or TaskStatus.Discarded
            or TaskStatus.Superseded;
    }

    private static string? Validate(TaskStatus from, TaskStatus to)
    {
        if (from == to)
        {
            return null;
        }

        return from switch
        {
            TaskStatus.Suggested => to is TaskStatus.Pending
                or TaskStatus.Discarded
                or TaskStatus.Superseded
                    ? null
                    : "Suggested tasks must be promoted, discarded, or superseded before execution writeback.",
            TaskStatus.Deferred => to is TaskStatus.Pending
                or TaskStatus.Discarded
                or TaskStatus.Superseded
                    ? null
                    : "Deferred tasks can only return to pending, be discarded, or be superseded.",
            TaskStatus.Pending => to is TaskStatus.Running
                or TaskStatus.Testing
                or TaskStatus.Review
                or TaskStatus.Completed
                or TaskStatus.Failed
                or TaskStatus.Blocked
                or TaskStatus.Deferred
                or TaskStatus.Discarded
                or TaskStatus.Superseded
                    ? null
                    : "Pending tasks must move through execution, review, completion, failure, blocking, deferral, discard, or supersession.",
            TaskStatus.Running => to is TaskStatus.Pending
                or TaskStatus.Testing
                or TaskStatus.Review
                or TaskStatus.ApprovalWait
                or TaskStatus.Completed
                or TaskStatus.Failed
                or TaskStatus.Blocked
                or TaskStatus.Superseded
                    ? null
                    : "Running tasks must settle to retry, testing, review, approval wait, completion, failure, blocking, or supersession.",
            TaskStatus.Testing => to is TaskStatus.Pending
                or TaskStatus.Review
                or TaskStatus.Completed
                or TaskStatus.Failed
                or TaskStatus.Blocked
                or TaskStatus.Superseded
                    ? null
                    : "Testing tasks must settle to retry, review, completion, failure, blocking, or supersession.",
            TaskStatus.ApprovalWait => to is TaskStatus.Pending
                or TaskStatus.Review
                or TaskStatus.Failed
                or TaskStatus.Blocked
                or TaskStatus.Superseded
                    ? null
                    : "Approval-wait tasks must resolve to dispatchable, review, failure, blocking, or supersession.",
            TaskStatus.Review => to is TaskStatus.Pending
                or TaskStatus.Completed
                or TaskStatus.Failed
                or TaskStatus.Blocked
                or TaskStatus.Superseded
                    ? null
                    : "Review tasks must resolve to pending, completed, failed, blocked, or superseded.",
            TaskStatus.Failed => to is TaskStatus.Pending
                or TaskStatus.Review
                or TaskStatus.Completed
                or TaskStatus.Blocked
                or TaskStatus.Discarded
                or TaskStatus.Superseded
                    ? null
                    : "Failed tasks can only be retried, reviewed, manually completed, blocked, discarded, or superseded.",
            TaskStatus.Blocked => to is TaskStatus.Pending
                or TaskStatus.Review
                or TaskStatus.Completed
                or TaskStatus.Failed
                or TaskStatus.Discarded
                or TaskStatus.Superseded
                    ? null
                    : "Blocked tasks can only be retried, reviewed, manually completed, failed, discarded, or superseded.",
            TaskStatus.Completed => to is TaskStatus.Review
                or TaskStatus.Merged
                    ? null
                    : "Completed tasks can only reopen review or advance to merged.",
            TaskStatus.Merged => to == TaskStatus.Review
                ? null
                : "Merged tasks can only reopen review.",
            TaskStatus.Discarded => "Discarded tasks are final and cannot be reopened through TaskGraph status mutation.",
            TaskStatus.Superseded => "Superseded tasks are final and cannot be reopened through TaskGraph status mutation.",
            _ => $"Unsupported source task status '{from}'.",
        };
    }
}
