using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.TaskGraph;

public sealed record TaskScheduleDecision(
    TaskScheduleDecisionKind Kind,
    string Reason,
    IReadOnlyList<TaskNode> Tasks,
    IReadOnlyList<TaskScheduleBlock> BlockedTasks,
    int ActiveWorkers,
    int MaxWorkers,
    TaskScheduleIdleReason IdleReason = TaskScheduleIdleReason.None)
{
    public TaskNode? Task => Tasks.FirstOrDefault();

    public bool ShouldDispatch => Kind == TaskScheduleDecisionKind.Dispatch && Tasks.Count > 0;

    public bool AllowsPlannerReentry => Kind == TaskScheduleDecisionKind.Idle && IdleReason == TaskScheduleIdleReason.NoReadyExecutionTask;

    public static TaskScheduleDecision Dispatch(
        IReadOnlyList<TaskNode> tasks,
        string reason,
        int activeWorkers,
        int maxWorkers,
        IReadOnlyList<TaskScheduleBlock>? blockedTasks = null)
    {
        return new TaskScheduleDecision(TaskScheduleDecisionKind.Dispatch, reason, tasks, blockedTasks ?? Array.Empty<TaskScheduleBlock>(), activeWorkers, maxWorkers);
    }

    public static TaskScheduleDecision Idle(
        string reason,
        int activeWorkers,
        int maxWorkers,
        IReadOnlyList<TaskScheduleBlock>? blockedTasks = null,
        TaskScheduleIdleReason idleReason = TaskScheduleIdleReason.None)
    {
        return new TaskScheduleDecision(TaskScheduleDecisionKind.Idle, reason, Array.Empty<TaskNode>(), blockedTasks ?? Array.Empty<TaskScheduleBlock>(), activeWorkers, maxWorkers, idleReason);
    }
}
