namespace Carves.Runtime.Application.TaskGraph;

public sealed class SchedulerDecisionException : InvalidOperationException
{
    public SchedulerDecisionException(string message)
        : base(message)
    {
    }
}
