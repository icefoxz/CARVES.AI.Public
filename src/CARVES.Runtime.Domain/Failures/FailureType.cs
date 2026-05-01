namespace Carves.Runtime.Domain.Failures;

public enum FailureType
{
    BuildFailure,
    TestRegression,
    ScopeDrift,
    InfinitePatchLoop,
    WrongFileTouched,
    ContractViolation,
    DependencyMisread,
    IncompleteTask,
    ReviewRejected,
    EnvironmentFailure,
    Timeout,
    Unknown,
}
