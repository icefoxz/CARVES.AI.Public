using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunPlanCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var surface = new LocalHostSurfaceService(services);
        if (arguments.Count == 0 || string.Equals(arguments[0], "status", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanStatus()));
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "init" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanStatusFromInitialization(arguments.Count >= 2 ? arguments[1] : null))),
            "packet" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanPacket())),
            "issue-workspace" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: plan issue-workspace <task-id>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanIssueWorkspaceResult(arguments[1]))),
            "submit-workspace" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: plan submit-workspace <task-id> [reason...]")
                : services.OperatorSurfaceService.SubmitManagedWorkspaceForReview(
                    arguments[1],
                    arguments.Count > 2 ? string.Join(' ', arguments.Skip(2)) : null),
            "export-card" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: plan export-card <json-path>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanExportResult(arguments[1]))),
            "export-packet" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: plan export-packet <json-path>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPlanExportPacketResult(arguments[1]))),
            _ => RunPlanCardCommand(services, arguments),
        };
    }

    private static OperatorCommandResult RunPlanCardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: plan-card <card-path> [--persist]");
        }

        var persist = arguments.Any(argument => string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase));
        var cardArgument = ResolvePrimaryArgument(arguments, [], ["--persist"]);
        if (string.IsNullOrWhiteSpace(cardArgument))
        {
            return OperatorCommandResult.Failure("Usage: plan-card <card-path> [--persist]");
        }

        var cardPath = ResolvePath(services.Paths.RepoRoot, cardArgument);
        var validation = services.OperatorSurfaceService.ValidateCard(cardPath, strict: false);
        if (validation.ExitCode != 0)
        {
            return validation;
        }

        return services.OperatorSurfaceService.PlanCard(cardPath, persist);
    }

    private static OperatorCommandResult RunSessionCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: session <start|status|tick|loop|pause|resume|stop> [...]");
        }

        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var subCommand = arguments[0];
        var remaining = arguments.Skip(1).Where(argument => !string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();

        return subCommand switch
        {
            "start" => services.OperatorSurfaceService.StartSession(dryRun),
            "status" => services.OperatorSurfaceService.SessionStatus(),
            "tick" => services.OperatorSurfaceService.TickSession(dryRun),
            "loop" => services.OperatorSurfaceService.RunSessionLoop(dryRun, ResolveOptionalPositiveInt(remaining, "--iterations", 5)),
            "pause" => remaining.Length == 0
                ? OperatorCommandResult.Failure("Usage: session pause <reason...>")
                : services.OperatorSurfaceService.PauseSession(string.Join(' ', remaining)),
            "resume" => services.OperatorSurfaceService.ResumeSession(remaining.Length == 0 ? "Session resumed through host." : string.Join(' ', remaining)),
            "stop" => services.OperatorSurfaceService.StopSession(remaining.Length == 0 ? "Session stopped through host." : string.Join(' ', remaining)),
            _ => OperatorCommandResult.Failure("Usage: session <start|status|tick|loop|pause|resume|stop> [...]"),
        };
    }

    private static OperatorCommandResult RunVerifyCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "runtime", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Failure("Usage: verify runtime");
        }

        return RuntimeConsistencyHostCommand.Execute(services, runningInsideResidentHost: true);
    }

    private static OperatorCommandResult RunReconcileCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "runtime", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Failure("Usage: reconcile runtime");
        }

        return services.OperatorSurfaceService.ReconcileRuntime();
    }

    private static OperatorCommandResult RunCleanupCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var includeRuntimeResidue = !arguments.Any(argument => string.Equals(argument, "--skip-runtime-residue", StringComparison.OrdinalIgnoreCase));
        var includeEphemeralResidue = !arguments.Any(argument => string.Equals(argument, "--skip-ephemeral-residue", StringComparison.OrdinalIgnoreCase));
        return services.OperatorSurfaceService.Cleanup(includeRuntimeResidue, includeEphemeralResidue);
    }

    private static OperatorCommandResult RunResetCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return arguments.Any(argument => string.Equals(argument, "--derived", StringComparison.OrdinalIgnoreCase))
            ? services.OperatorSurfaceService.RuntimeResetDerived()
            : OperatorCommandResult.Failure("Usage: reset --derived");
    }

    private static OperatorCommandResult RunStatusCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Any(argument => string.Equals(argument, "--runs", StringComparison.OrdinalIgnoreCase)))
        {
            return services.OperatorSurfaceService.StatusRuns();
        }

        if (arguments.Any(argument => string.Equals(argument, "--summary", StringComparison.OrdinalIgnoreCase)))
        {
            return services.OperatorSurfaceService.StatusSummary();
        }

        if (arguments.Any(argument => string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase)))
        {
            return services.OperatorSurfaceService.StatusFull();
        }

        return services.OperatorSurfaceService.Status();
    }

    private static OperatorCommandResult RunAuditCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: audit <runtime-noise|codegraph|sustainability>");
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "runtime-noise" => services.OperatorSurfaceService.AuditRuntimeNoise(),
            "codegraph" => services.OperatorSurfaceService.AuditCodeGraph(arguments.Any(argument => string.Equals(argument, "--strict", StringComparison.OrdinalIgnoreCase))),
            "sustainability" => services.OperatorSurfaceService.AuditSustainability(),
            _ => OperatorCommandResult.Failure("Usage: audit <runtime-noise|codegraph|sustainability>"),
        };
    }

    private static OperatorCommandResult RunReportCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: report <delegation|approvals> [--hours <n>]");
        }

        return arguments[0] switch
        {
            "delegation" => services.OperatorSurfaceService.DelegationReport(ResolveOptionalPositiveInt(arguments, "--hours", 24)),
            "approvals" => services.OperatorSurfaceService.ApprovalReport(ResolveOptionalPositiveInt(arguments, "--hours", 24)),
            _ => OperatorCommandResult.Failure("Usage: report <delegation|approvals> [--hours <n>]"),
        };
    }

    private static OperatorCommandResult RunFailuresCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var taskId = ResolveOption(arguments, "--task");
        var summaryOnly = arguments.Any(argument => string.Equals(argument, "--summary", StringComparison.OrdinalIgnoreCase));
        if (arguments.Any(argument =>
                !string.Equals(argument, "--summary", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(argument, "--task", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(argument, taskId, StringComparison.Ordinal)))
        {
            return OperatorCommandResult.Failure("Usage: failures [--task <task-id>] [--summary]");
        }

        return services.OperatorSurfaceService.Failures(taskId, summaryOnly);
    }

    private static OperatorCommandResult RunPlannerCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: planner <status|run|host|wake|sleep> [...]");
        }

        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var filtered = arguments.Where(argument => !string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered[0] switch
        {
            "status" => services.OperatorSurfaceService.PlannerStatus(),
            "run" => services.OperatorSurfaceService.PlannerRun(
                dryRun,
                ParsePlannerWakeReason(ResolveOption(filtered, "--wake"), PlannerWakeReason.ExplicitHumanWake),
                ResolveOption(filtered, "--detail") ?? "planner run requested through host"),
            "host" => services.OperatorSurfaceService.PlannerLoop(
                dryRun,
                ResolveOptionalPositiveInt(filtered, "--iterations", 3),
                ParsePlannerWakeReason(ResolveOption(filtered, "--wake"), PlannerWakeReason.ExplicitHumanWake),
                ResolveOption(filtered, "--detail") ?? "planner host loop requested through host"),
            "wake" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: planner wake <reason> <detail...>")
                : services.OperatorSurfaceService.PlannerWake(ParsePlannerWakeReason(filtered[1], PlannerWakeReason.ExplicitHumanWake), string.Join(' ', filtered.Skip(2))),
            "sleep" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: planner sleep <reason> <detail...>")
                : services.OperatorSurfaceService.PlannerSleep(ParsePlannerSleepReason(filtered[1], PlannerSleepReason.WaitingForHumanAction), string.Join(' ', filtered.Skip(2))),
            _ => OperatorCommandResult.Failure("Usage: planner <status|run|host|wake|sleep> [...]"),
        };
    }
}
