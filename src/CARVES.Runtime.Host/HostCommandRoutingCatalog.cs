namespace Carves.Runtime.Host;

internal static class HostCommandRoutingCatalog
{
    internal const string MemoryTruthWriteFamily = "memory-truth-write";
    internal const string PlanningApprovalFamily = "planning-approval";
    internal const string TaskExecutionFamily = "task-execution";
    internal const string ReviewWritebackFamily = "review-writeback";
    internal const string RuntimeStateRepairFamily = "runtime-state-repair";
    internal const string ActorSessionRegistrationFamily = "actor-session-registration";
    internal const string WorkerPolicyActivationFamily = "worker-policy-activation";

    private sealed record StaticCommandRoute(string Capability, bool EmitFallbackNotice);

    private static readonly IReadOnlyDictionary<string, StaticCommandRoute> StaticRoutes =
        new Dictionary<string, StaticCommandRoute>(StringComparer.OrdinalIgnoreCase)
        {
            ["approve-task"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["review-task"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["approve-review"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["reject-review"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["reopen-review"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["sync-state"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["supersede-card-tasks"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["status"] = new("runtime-status", EmitFallbackNotice: true),
            ["session"] = new("runtime-status", EmitFallbackNotice: true),
            ["repo"] = new("runtime-status", EmitFallbackNotice: true),
            ["runtime"] = new("runtime-status", EmitFallbackNotice: true),
            ["provider"] = new("runtime-status", EmitFallbackNotice: true),
            ["governance"] = new("runtime-status", EmitFallbackNotice: true),
            ["api"] = new("runtime-status", EmitFallbackNotice: false),
            ["report"] = new("runtime-status", EmitFallbackNotice: true),
            ["failures"] = new("runtime-status", EmitFallbackNotice: true),
            ["cleanup"] = new("runtime-status", EmitFallbackNotice: true),
            ["compact-history"] = new("runtime-status", EmitFallbackNotice: true),
            ["audit"] = new("runtime-status", EmitFallbackNotice: true),
            ["verify"] = new("runtime-status", EmitFallbackNotice: true),
            ["reconcile"] = new("runtime-status", EmitFallbackNotice: true),
            ["repair"] = new("runtime-status", EmitFallbackNotice: true),
            ["rebuild"] = new("runtime-status", EmitFallbackNotice: true),
            ["reset"] = new("runtime-status", EmitFallbackNotice: true),
            ["policy"] = new("runtime-status", EmitFallbackNotice: true),
            ["validation"] = new("worker-inspect", EmitFallbackNotice: true),
            ["create-card-draft"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["create-taskgraph-draft"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["approve-taskgraph-draft"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["update-card"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["approve-card"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["reject-card"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["archive-card"] = new("control-plane-mutation", EmitFallbackNotice: true),
            ["approve-suggested-task"] = new("control-plane-mutation", EmitFallbackNotice: false),
            ["list-cards"] = new("card-task-inspect", EmitFallbackNotice: true),
            ["inspect-card"] = new("card-task-inspect", EmitFallbackNotice: true),
            ["pilot"] = new("card-task-inspect", EmitFallbackNotice: false),
            ["planner"] = new("planner-inspect", EmitFallbackNotice: true),
            ["worker"] = new("worker-inspect", EmitFallbackNotice: true),
            ["actor"] = new("runtime-status", EmitFallbackNotice: true),
            ["dashboard"] = new("dashboard", EmitFallbackNotice: true),
            ["workbench"] = new("workbench", EmitFallbackNotice: true),
            ["attach"] = new("attach-flow", EmitFallbackNotice: true),
            ["intent"] = new("interaction-surface", EmitFallbackNotice: true),
            ["protocol"] = new("interaction-surface", EmitFallbackNotice: true),
            ["prompt"] = new("interaction-surface", EmitFallbackNotice: true),
            ["context"] = new("card-task-inspect", EmitFallbackNotice: true),
            ["evidence"] = new("card-task-inspect", EmitFallbackNotice: true),
            ["inspect"] = new("card-task-inspect", EmitFallbackNotice: true),
            ["discuss"] = new("discussion-surface", EmitFallbackNotice: false),
            ["agent"] = new("agent-gateway", EmitFallbackNotice: false),
            ["console"] = new("runtime-status", EmitFallbackNotice: true),
        };

    public static string? ResolveCapability(string command, IReadOnlyList<string> arguments)
    {
        if (string.Equals(command, "agent", StringComparison.OrdinalIgnoreCase)
            && arguments.Count > 0
            && (string.Equals(arguments[0], "start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "boot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "handoff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "bootstrap", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var minimumStatefulActionFamily = ResolveMinimumStatefulActionFamily(command, arguments);
        var minimumStatefulActionCapability = ResolveRequiredCapabilityForMinimumStatefulActionFamily(minimumStatefulActionFamily);
        if (minimumStatefulActionCapability is not null)
        {
            return minimumStatefulActionCapability;
        }

        var readOnlySurfaceCapability = ResolveReadOnlySurfaceCapability(command, arguments);
        if (readOnlySurfaceCapability is not null)
        {
            return readOnlySurfaceCapability;
        }

        if (StaticRoutes.TryGetValue(command, out var route))
        {
            return route.Capability;
        }

        return command switch
        {
            "plan" => ResolvePlanCapability(arguments),
            "plan-card" => arguments.Any(argument => string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase))
                ? "control-plane-mutation"
                : "planner-inspect",
            "card" => ResolveCardCapability(arguments),
            "memory" => ResolveMemoryCapability(arguments),
            "task" => ResolveTaskCapability(arguments),
            "run" => arguments.Count > 0 && string.Equals(arguments[0], "task", StringComparison.OrdinalIgnoreCase)
                ? "delegated-execution"
                : null,
            _ => null,
        };
    }

    public static bool ShouldEmitFallbackNotice(string command)
    {
        if (StaticRoutes.TryGetValue(command, out var route))
        {
            return route.EmitFallbackNotice;
        }

        return command is "plan" or "plan-card";
    }

    private static string? ResolveReadOnlySurfaceCapability(string command, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return null;
        }

        if (string.Equals(command, "api", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(arguments[0], "worker-supervisor-instances", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "worker-supervisor-events", StringComparison.OrdinalIgnoreCase)))
        {
            return "worker-inspect";
        }

        if (string.Equals(command, "worker", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(arguments[0], "supervisor-instances", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "supervisor-events", StringComparison.OrdinalIgnoreCase)))
        {
            return "worker-inspect";
        }

        return null;
    }

    public static bool IsDelegatedExecution(string command, IReadOnlyList<string> arguments)
    {
        if (string.Equals(command, "task", StringComparison.OrdinalIgnoreCase))
        {
            return arguments.Count > 0 && string.Equals(arguments[0], "run", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(command, "run", StringComparison.OrdinalIgnoreCase)
            && arguments.Count > 0
            && string.Equals(arguments[0], "task", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveMinimumStatefulActionFamily(string command, IReadOnlyList<string> arguments)
    {
        if (string.Equals(command, "intent", StringComparison.OrdinalIgnoreCase))
        {
            return arguments.Count > 0 && string.Equals(arguments[0], "accept", StringComparison.OrdinalIgnoreCase)
                ? MemoryTruthWriteFamily
                : null;
        }

        if (string.Equals(command, "memory", StringComparison.OrdinalIgnoreCase))
        {
            return arguments.Count > 0 && string.Equals(arguments[0], "promote", StringComparison.OrdinalIgnoreCase)
                ? MemoryTruthWriteFamily
                : null;
        }

        if (string.Equals(command, "actor", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count == 0)
            {
                return null;
            }

            if (string.Equals(arguments[0], "repair-sessions", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "clear-sessions", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeStateRepairFamily;
            }

            return string.Equals(arguments[0], "register", StringComparison.OrdinalIgnoreCase)
                ? ActorSessionRegistrationFamily
                : null;
        }

        if (string.Equals(command, "worker", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count == 0)
            {
                return null;
            }

            if (string.Equals(arguments[0], "supervisor-archive", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeStateRepairFamily;
            }

            if (string.Equals(arguments[0], "supervisor-launch", StringComparison.OrdinalIgnoreCase))
            {
                return ActorSessionRegistrationFamily;
            }

            return string.Equals(arguments[0], "activate-external-codex", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(arguments[0], "activate-external-app-cli", StringComparison.OrdinalIgnoreCase)
                ? WorkerPolicyActivationFamily
                : null;
        }

        if (string.Equals(command, "api", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count == 0)
            {
                return null;
            }

            if (string.Equals(arguments[0], "worker-automation-schedule-tick", StringComparison.OrdinalIgnoreCase)
                && arguments.Any(argument => string.Equals(argument, "--dispatch", StringComparison.OrdinalIgnoreCase)))
            {
                return TaskExecutionFamily;
            }

            if (string.Equals(arguments[0], "actor-session-repair", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "actor-session-clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "actor-session-stop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "actor-session-heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeStateRepairFamily;
            }

            if (string.Equals(arguments[0], "worker-external-codex-activation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "worker-external-app-cli-activation", StringComparison.OrdinalIgnoreCase))
            {
                return WorkerPolicyActivationFamily;
            }

            if (string.Equals(arguments[0], "worker-supervisor-archive", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeStateRepairFamily;
            }

            if (string.Equals(arguments[0], "worker-supervisor-launch", StringComparison.OrdinalIgnoreCase))
            {
                return ActorSessionRegistrationFamily;
            }

            return string.Equals(arguments[0], "actor-session-register", StringComparison.OrdinalIgnoreCase)
                ? ActorSessionRegistrationFamily
                : null;
        }

        if (string.Equals(command, "approve-card", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "approve-taskgraph-draft", StringComparison.OrdinalIgnoreCase))
        {
            return PlanningApprovalFamily;
        }

        if (string.Equals(command, "card", StringComparison.OrdinalIgnoreCase)
            && arguments.Count >= 3
            && string.Equals(arguments[0], "status", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[2], "approved", StringComparison.OrdinalIgnoreCase))
        {
            return PlanningApprovalFamily;
        }

        if (IsDelegatedExecution(command, arguments))
        {
            return TaskExecutionFamily;
        }

        if (string.Equals(command, "review-task", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "approve-review", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewWritebackFamily;
        }

        return null;
    }

    public static string? ResolveRequiredCapabilityForMinimumStatefulActionFamily(string? family)
    {
        return family switch
        {
            MemoryTruthWriteFamily or PlanningApprovalFamily or ReviewWritebackFamily or RuntimeStateRepairFamily or ActorSessionRegistrationFamily or WorkerPolicyActivationFamily => "control-plane-mutation",
            TaskExecutionFamily => "delegated-execution",
            _ => null,
        };
    }

    private static string ResolveCardCapability(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return "card-task-inspect";
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "create-draft" or "update" or "status" => "control-plane-mutation",
            _ => "card-task-inspect",
        };
    }

    private static string ResolveTaskCapability(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return "card-task-inspect";
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "run" => "delegated-execution",
            "ingest-result" or "retry" => "control-plane-mutation",
            _ => "card-task-inspect",
        };
    }

    private static string ResolveMemoryCapability(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return "card-task-inspect";
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "promote" => "control-plane-mutation",
            _ => "card-task-inspect",
        };
    }

    private static string ResolvePlanCapability(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return "planner-inspect";
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "status" => "planner-inspect",
            "packet" => "planner-inspect",
            "init" => "interaction-surface",
            "issue-workspace" => "control-plane-mutation",
            "submit-workspace" => "control-plane-mutation",
            "export-card" => "interaction-surface",
            "export-packet" => "interaction-surface",
            _ => arguments.Any(argument => string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase))
                ? "control-plane-mutation"
                : "planner-inspect",
        };
    }
}
