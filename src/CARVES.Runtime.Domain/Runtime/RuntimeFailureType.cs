namespace Carves.Runtime.Domain.Runtime;

public enum RuntimeFailureType
{
    WorkerExecutionFailure,
    SchedulerDecisionFailure,
    ArtifactPersistenceFailure,
    ReviewRejected,
    SchemaMismatch,
    ControlPlaneDesync,
    InvariantViolation,
}
