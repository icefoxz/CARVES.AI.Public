namespace Carves.Runtime.Domain.Runtime;

public static class RuntimeActionabilitySemantics
{
    public static RuntimeActionability ResolveCurrent(RuntimeSessionState session)
    {
        if (session.StopReason is not null && session.StopActionability != RuntimeActionability.None)
        {
            return session.StopActionability;
        }

        if (session.WaitingReason is not null && session.WaitingActionability != RuntimeActionability.None)
        {
            return session.WaitingActionability;
        }

        return session.LoopActionability;
    }

    public static string Describe(RuntimeActionability actionability)
    {
        return actionability switch
        {
            RuntimeActionability.PlannerActionable => "planner",
            RuntimeActionability.WorkerActionable => "worker",
            RuntimeActionability.HumanActionable => "human",
            RuntimeActionability.Terminal => "terminal",
            _ => "none",
        };
    }

    public static string DescribeNextAction(RuntimeSessionState session)
    {
        return ResolveCurrent(session) switch
        {
            RuntimeActionability.PlannerActionable => "planner re-entry and opportunity evaluation",
            RuntimeActionability.WorkerActionable => "schedule or execute eligible tasks",
            RuntimeActionability.HumanActionable when session.Status == RuntimeSessionStatus.ApprovalWait => "approve, deny, or timeout pending worker permission requests",
            RuntimeActionability.HumanActionable when session.Status == RuntimeSessionStatus.ReviewWait => "approve or reject pending review work",
            RuntimeActionability.HumanActionable => "operator review or governance decision",
            RuntimeActionability.Terminal => "start a new session or inspect terminal state",
            _ => "observe runtime state",
        };
    }
}
