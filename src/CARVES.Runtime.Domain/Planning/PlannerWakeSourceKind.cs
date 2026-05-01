namespace Carves.Runtime.Domain.Planning;

public enum PlannerWakeSourceKind
{
    None,
    WorkerOutcome,
    PermissionResolution,
    ReviewResolution,
    DependencyUnlock,
    Human,
    Runtime,
}
