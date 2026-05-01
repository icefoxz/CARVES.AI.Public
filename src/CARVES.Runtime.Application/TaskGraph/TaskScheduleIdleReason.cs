namespace Carves.Runtime.Application.TaskGraph;

public enum TaskScheduleIdleReason
{
    None,
    SessionPaused,
    SessionStopped,
    WorkerPoolAtCapacity,
    ReviewBoundary,
    NoReadyExecutionTask,
    AllCandidatesBlocked,
}
