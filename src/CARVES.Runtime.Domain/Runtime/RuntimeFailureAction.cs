namespace Carves.Runtime.Domain.Runtime;

public enum RuntimeFailureAction
{
    RetryTask,
    AbortTask,
    PauseSession,
    EscalateToOperator,
    RebuildWorktree,
    SwitchProvider,
    BlockTask,
}
