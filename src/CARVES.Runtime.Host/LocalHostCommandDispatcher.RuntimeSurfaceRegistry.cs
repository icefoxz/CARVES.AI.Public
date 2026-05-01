using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static readonly string[] FixedInspectCommandsRequiringArgument =
    [
        "methodology",
        "card",
        "card-draft",
        "task",
        "taskgraph-draft",
        "run",
        "validation-trace",
        "context-pack",
        "execution-budget",
        "execution-risk",
        "boundary",
        "execution-pattern",
        "execution-trace",
        "worker-dispatch-pilot-evidence",
        "replan",
        "suggested-tasks",
        "execution-memory",
        "attach-proof",
        "pilot-evidence"
    ];

    internal static readonly string[] FixedInspectUsageCommands =
    [
        "card",
        "card-draft",
        "task",
        "taskgraph-draft",
        "run",
        "dispatch",
        "worker-automation-readiness",
        "worker-automation-schedule-tick",
        "routing-profile",
        "qualification",
        "qualification-candidate",
        "qualification-promotion",
        "promotion-readiness",
        "validation-suite",
        "validation-trace",
        "validation-summary",
        "validation-history",
        "validation-coverage",
        "context-pack",
        "execution-budget",
        "execution-risk",
        "boundary",
        "execution-pattern",
        "execution-trace",
        "worker-dispatch-pilot-evidence",
        "replan",
        "suggested-tasks",
        "execution-memory",
        "attach-proof",
        "pilot-evidence",
        "methodology",
        "async-resume-gate"
    ];

    internal static readonly string[] FixedApiUsageCommands =
    [
        "platform-status",
        "repos",
        "repo-runtime",
        "repo-tasks",
        "repo-opportunities",
        "repo-session",
        "provider-quota",
        "provider-route",
        "routing-profile",
        "qualification",
        "qualification-candidate",
        "qualification-promotion",
        "promotion-readiness",
        "validation-suite",
        "validation-trace",
        "validation-summary",
        "validation-history",
        "validation-coverage",
        "platform-schedule",
        "worker-providers",
        "worker-profiles",
        "worker-selection",
        "worker-health",
        "worker-automation-readiness",
        "worker-automation-schedule-tick",
        "worker-supervisor-launch",
        "worker-supervisor-instances",
        "worker-supervisor-events",
        "worker-supervisor-archive",
        "worker-dispatch-pilot-evidence",
        "worker-external-app-cli-activation",
        "worker-external-codex-activation",
        "worker-permissions",
        "worker-permission-audit",
        "worker-incidents",
        "worker-leases",
        "operational-summary",
        "governance-report",
        "actor-sessions",
        "actor-session-register",
        "actor-session-repair",
        "actor-session-clear",
        "actor-session-stop",
        "actor-session-heartbeat",
        "actor-session-fallback-policy",
        "actor-ownership",
        "os-events",
        "agent-gateway-trace",
        "repo-gateway"
    ];

    private static string BuildInspectUsage()
    {
        return $"Usage: inspect <{string.Join('|', FixedInspectUsageCommands.Concat(RuntimeSurfaceCommandRegistry.DefaultVisibleCommandNames).Concat(["all-surfaces"]))}> [<id>]";
    }

    private static string BuildApiUsage()
    {
        return $"Usage: api <{string.Join('|', FixedApiUsageCommands.Concat(RuntimeSurfaceCommandRegistry.DefaultVisibleCommandNames).Concat(["all-surfaces"]))}> [...]";
    }

    private static string BuildInspectAllSurfacesUsage()
    {
        return BuildAllSurfacesUsage("inspect", FixedInspectUsageCommands, "[<id>]");
    }

    private static string BuildApiAllSurfacesUsage()
    {
        return BuildAllSurfacesUsage("api", FixedApiUsageCommands, "[...]");
    }

    private static string BuildAllSurfacesUsage(string verb, IEnumerable<string> fixedCommands, string argumentTail)
    {
        var lines = new List<string>
        {
            $"Usage: {verb} <{string.Join('|', fixedCommands.Concat(RuntimeSurfaceCommandRegistry.CommandNames))}> {argumentTail}",
        };

        if (RuntimeSurfaceCommandRegistry.CompatibilityAliasCommandMetadata.Count > 0)
        {
            lines.Add("Compatibility aliases:");
            lines.AddRange(RuntimeSurfaceCommandRegistry.CompatibilityAliasCommandMetadata.Select(command =>
                $"  {command.Name} -> {command.SuccessorSurfaceId ?? "(none)"}; retirement={ToToken(command.RetirementPosture)}; exact_invocation=preserved"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ToToken(RuntimeSurfaceRetirementPosture posture)
    {
        return posture switch
        {
            RuntimeSurfaceRetirementPosture.AliasRetained => "alias_retained",
            _ => "active_primary",
        };
    }

    private static bool FixedInspectCommandRequiresArgument(string command)
    {
        return FixedInspectCommandsRequiringArgument.Contains(command, StringComparer.OrdinalIgnoreCase);
    }
}
