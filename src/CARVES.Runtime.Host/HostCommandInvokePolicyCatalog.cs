using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Host;

internal sealed record HostCommandInvokePolicy(
    TimeSpan RequestTimeout,
    bool UseAcceptedOperationPolling,
    TimeSpan PollInterval,
    TimeSpan BaseWait,
    TimeSpan StallTimeout,
    TimeSpan MaxWait);

internal static class HostCommandInvokePolicyCatalog
{
    private static readonly TimeSpan AggregateSurfaceReadTimeout = TimeSpan.FromSeconds(30);

    public static HostCommandInvokePolicy Resolve(HostInvokeRuntimePolicy policy, string command, IReadOnlyList<string> arguments)
    {
        var minimumStatefulActionFamily = HostCommandRoutingCatalog.ResolveMinimumStatefulActionFamily(command, arguments);
        var minimumStatefulActionCapability = HostCommandRoutingCatalog.ResolveRequiredCapabilityForMinimumStatefulActionFamily(minimumStatefulActionFamily);
        if (string.Equals(minimumStatefulActionCapability, "control-plane-mutation", StringComparison.OrdinalIgnoreCase))
        {
            return ToInvokePolicy(policy.ControlPlaneMutation);
        }

        if (string.Equals(minimumStatefulActionCapability, "delegated-execution", StringComparison.OrdinalIgnoreCase))
        {
            return ToInvokePolicy(policy.DelegatedExecution);
        }

        var capability = HostCommandRoutingCatalog.ResolveCapability(command, arguments);
        if (string.Equals(capability, "control-plane-mutation", StringComparison.OrdinalIgnoreCase))
        {
            var invokePolicy = ToInvokePolicy(policy.ControlPlaneMutation);
            return string.Equals(command, "sync-state", StringComparison.OrdinalIgnoreCase)
                ? EnforceSyncStateMinimums(invokePolicy)
                : invokePolicy;
        }

        if (string.Equals(capability, "attach-flow", StringComparison.OrdinalIgnoreCase))
        {
            return ToInvokePolicy(policy.AttachFlow);
        }

        if (HostCommandRoutingCatalog.IsDelegatedExecution(command, arguments))
        {
            return ToInvokePolicy(policy.DelegatedExecution);
        }

        var defaultReadPolicy = ToInvokePolicy(policy.DefaultRead);
        return RequiresAggregateSurfaceReadBudget(command, arguments)
            ? EnforceAggregateSurfaceReadMinimums(defaultReadPolicy)
            : defaultReadPolicy;
    }

    private static HostCommandInvokePolicy ToInvokePolicy(HostInvokeClassRuntimePolicy policy)
    {
        return new HostCommandInvokePolicy(
            RequestTimeout: TimeSpan.FromSeconds(policy.RequestTimeoutSeconds),
            UseAcceptedOperationPolling: policy.UseAcceptedOperationPolling,
            PollInterval: TimeSpan.FromMilliseconds(policy.PollIntervalMs),
            BaseWait: TimeSpan.FromSeconds(policy.BaseWaitSeconds),
            StallTimeout: TimeSpan.FromSeconds(policy.StallTimeoutSeconds),
            MaxWait: TimeSpan.FromSeconds(policy.MaxWaitSeconds));
    }

    private static HostCommandInvokePolicy EnforceSyncStateMinimums(HostCommandInvokePolicy policy)
    {
        var pollInterval = policy.PollInterval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(250) : policy.PollInterval;
        var baseWait = policy.BaseWait < TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : policy.BaseWait;
        var stallTimeout = policy.StallTimeout < TimeSpan.FromSeconds(20) ? TimeSpan.FromSeconds(20) : policy.StallTimeout;
        var maxWait = policy.MaxWait < TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : policy.MaxWait;

        if (stallTimeout > maxWait)
        {
            stallTimeout = maxWait;
        }

        if (baseWait > maxWait)
        {
            baseWait = maxWait;
        }

        return policy with
        {
            UseAcceptedOperationPolling = true,
            PollInterval = pollInterval,
            BaseWait = baseWait,
            StallTimeout = stallTimeout,
            MaxWait = maxWait,
        };
    }

    private static bool RequiresAggregateSurfaceReadBudget(string command, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return false;
        }

        if ((string.Equals(command, "inspect", StringComparison.OrdinalIgnoreCase)
             || string.Equals(command, "api", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(arguments[0], "runtime-agent-thread-start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "worker-automation-schedule-tick", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(command, "agent", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(arguments[0], "start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "boot", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(command, "pilot", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(arguments[0], "boot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "agent-start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "thread-start", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static HostCommandInvokePolicy EnforceAggregateSurfaceReadMinimums(HostCommandInvokePolicy policy)
    {
        return policy.RequestTimeout >= AggregateSurfaceReadTimeout
            ? policy
            : policy with { RequestTimeout = AggregateSurfaceReadTimeout };
    }
}
