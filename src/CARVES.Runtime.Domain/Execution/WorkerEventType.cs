namespace Carves.Runtime.Domain.Execution;

public enum WorkerEventType
{
    RunStarted = 0,
    TurnStarted = 1,
    TurnCompleted = 2,
    TurnFailed = 3,
    CommandExecuted = 4,
    FileEditObserved = 5,
    ValidationObserved = 6,
    PermissionRequested = 7,
    ApprovalWait = 8,
    FinalSummary = 9,
    RawError = 10,
}
