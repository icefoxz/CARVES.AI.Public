namespace Carves.Runtime.Application.TaskGraph;

public enum TaskConflictKind
{
    ScopeOverlap,
    ReviewPending,
    AlreadyRunning,
}
