namespace Carves.Runtime.Application.TaskGraph;

public enum TaskScheduleBlockKind
{
    Conflict,
    ConcurrencyCap,
    Governance,
    ReviewBoundary,
    TaskType,
}
