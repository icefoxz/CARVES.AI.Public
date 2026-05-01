namespace Carves.Runtime.Domain.Execution;

public enum WorkerRecoveryAction
{
    None = 0,
    Retry = 1,
    RebuildWorktree = 2,
    SwitchProvider = 3,
    BlockTask = 4,
    EscalateToOperator = 5,
}
