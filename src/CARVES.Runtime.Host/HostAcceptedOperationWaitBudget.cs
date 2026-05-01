namespace Carves.Runtime.Host;

internal static class HostAcceptedOperationWaitBudget
{
    public static DateTimeOffset ComputeInitialDeadline(DateTimeOffset acceptedAt, HostCommandInvokePolicy policy)
    {
        return Min(acceptedAt.Add(policy.BaseWait), acceptedAt.Add(policy.MaxWait));
    }

    public static DateTimeOffset ComputeAdaptiveDeadline(
        DateTimeOffset acceptedAt,
        DateTimeOffset lastProgressAt,
        HostCommandInvokePolicy policy)
    {
        var baseDeadline = acceptedAt.Add(policy.BaseWait);
        var progressDeadline = lastProgressAt.Add(policy.StallTimeout);
        return Min(Max(baseDeadline, progressDeadline), acceptedAt.Add(policy.MaxWait));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right ? left : right;
    }
}
