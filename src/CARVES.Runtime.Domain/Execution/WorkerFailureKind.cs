namespace Carves.Runtime.Domain.Execution;

public enum WorkerFailureKind
{
    None = 0,
    TransientInfra = 1,
    EnvironmentBlocked = 2,
    PolicyDenied = 3,
    TaskLogicFailed = 4,
    LaunchFailure = 5,
    AttachFailure = 6,
    WrapperFailure = 7,
    ArtifactFailure = 8,
    BuildFailure = 9,
    TestFailure = 10,
    ContractFailure = 11,
    PatchFailure = 12,
    Timeout = 13,
    Cancelled = 14,
    Aborted = 15,
    InvalidOutput = 16,
    ApprovalRequired = 17,
    Unknown = 18,
}
