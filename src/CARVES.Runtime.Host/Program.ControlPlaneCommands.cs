using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static OperatorCommandResult RunVerifyCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "runtime", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Failure("Usage: verify runtime");
        }

        return RuntimeConsistencyHostCommand.Execute(services, runningInsideResidentHost: false);
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

    private static OperatorCommandResult RunFleetCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "status", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Failure("Usage: fleet status [--full]");
        }

        return services.OperatorSurfaceService.FleetStatus(arguments.Any(argument => string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase)));
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
            return OperatorCommandResult.Failure("Usage: audit <runtime-noise|codegraph|sustainability> [--strict]");
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "runtime-noise" => services.OperatorSurfaceService.AuditRuntimeNoise(),
            "codegraph" => services.OperatorSurfaceService.AuditCodeGraph(arguments.Any(argument => string.Equals(argument, "--strict", StringComparison.OrdinalIgnoreCase))),
            "sustainability" => services.OperatorSurfaceService.AuditSustainability(),
            _ => OperatorCommandResult.Failure("Usage: audit <runtime-noise|codegraph|sustainability> [--strict]"),
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
            "delegation" => services.OperatorSurfaceService.DelegationReport(ResolveOptionalPositiveInt(arguments, "--hours", defaultValue: 24)),
            "approvals" => services.OperatorSurfaceService.ApprovalReport(ResolveOptionalPositiveInt(arguments, "--hours", defaultValue: 24)),
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

    private static OperatorCommandResult RunInspectCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return LocalHostCommandDispatcher.Dispatch(services, "inspect", arguments);
    }

    private static OperatorCommandResult RunPlanCard(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: plan-card <card-path> [--persist]");
        }

        var persist = arguments.Any(argument => string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase));
        var cardArgument = arguments.First(argument => !string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase));
        var cardPath = ResolvePath(services.Paths.RepoRoot, cardArgument);
        var validation = services.OperatorSurfaceService.ValidateCard(cardPath, strict: false);
        if (validation.ExitCode != 0)
        {
            return validation;
        }

        return services.OperatorSurfaceService.PlanCard(cardPath, persist);
    }

    private static OperatorCommandResult RunApproveTask(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return arguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: approve-task <task-id>")
            : services.OperatorSurfaceService.ApproveTask(arguments[0]);
    }

    private static OperatorCommandResult RunReviewTask(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        if (filteredArguments.Count < 3)
        {
            return OperatorCommandResult.Failure("Usage: review-task <task-id> <verdict> <reason...> [--actor-kind <kind>] [--actor-identity <id>]");
        }

        return actorKind is null
            ? services.OperatorSurfaceService.ReviewTask(filteredArguments[0], filteredArguments[1], string.Join(' ', filteredArguments.Skip(2)))
            : services.OperatorSurfaceService.ReviewTaskAsActor(filteredArguments[0], filteredArguments[1], string.Join(' ', filteredArguments.Skip(2)), actorKind.Value, actorIdentity!);
    }

    private static OperatorCommandResult RunApproveReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        if (filteredArguments.Count < 2)
        {
            return OperatorCommandResult.Failure("Usage: approve-review <task-id> [--provisional] <reason...> [--actor-kind <kind>] [--actor-identity <id>]");
        }

        var provisional = filteredArguments.Any(argument => string.Equals(argument, "--provisional", StringComparison.OrdinalIgnoreCase));
        var filtered = filteredArguments.Where(argument => !string.Equals(argument, "--provisional", StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered.Length < 2
            ? OperatorCommandResult.Failure("Usage: approve-review <task-id> [--provisional] <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.ApproveReview(
                    filtered[0],
                    string.Join(' ', filtered.Skip(1)),
                    provisional: provisional)
                : services.OperatorSurfaceService.ApproveReviewAsActor(
                    filtered[0],
                    string.Join(' ', filtered.Skip(1)),
                    actorKind.Value,
                    actorIdentity!,
                    provisional: provisional);
    }

    private static OperatorCommandResult RunRejectReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: reject-review <task-id> <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.RejectReview(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)))
                : services.OperatorSurfaceService.RejectReviewAsActor(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)), actorKind.Value, actorIdentity!);
    }

    private static OperatorCommandResult RunReopenReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: reopen-review <task-id> <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.ReopenReview(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)))
                : services.OperatorSurfaceService.ReopenReviewAsActor(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)), actorKind.Value, actorIdentity!);
    }

    private static OperatorCommandResult RunSessionCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: session <start|status|tick|pause|resume|stop> [...]");
        }

        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var subCommand = arguments[0];
        var remaining = arguments.Skip(1).Where(argument => !string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();

        return subCommand switch
        {
            "start" => services.OperatorSurfaceService.StartSession(dryRun),
            "status" => services.OperatorSurfaceService.SessionStatus(),
            "tick" => services.OperatorSurfaceService.TickSession(dryRun),
            "loop" => services.OperatorSurfaceService.RunSessionLoop(dryRun, ResolveIterationCount(remaining)),
            "pause" => remaining.Length == 0
                ? OperatorCommandResult.Failure("Usage: session pause <reason...>")
                : services.OperatorSurfaceService.PauseSession(string.Join(' ', remaining)),
            "resume" => services.OperatorSurfaceService.ResumeSession(remaining.Length == 0 ? "Session resumed through operator surface." : string.Join(' ', remaining)),
            "stop" => services.OperatorSurfaceService.StopSession(remaining.Length == 0 ? "Session stopped through operator surface." : string.Join(' ', remaining)),
            _ => OperatorCommandResult.Failure("Usage: session <start|status|tick|loop|pause|resume|stop> [...]"),
        };
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
                ResolveOption(filtered, "--detail") ?? "planner run requested by operator"),
            "host" => services.OperatorSurfaceService.PlannerLoop(
                dryRun,
                ResolveOptionalPositiveInt(filtered, "--iterations", defaultValue: 3),
                ParsePlannerWakeReason(ResolveOption(filtered, "--wake"), PlannerWakeReason.ExplicitHumanWake),
                ResolveOption(filtered, "--detail") ?? "planner host loop requested by operator"),
            "wake" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: planner wake <reason> <detail...>")
                : services.OperatorSurfaceService.PlannerWake(ParsePlannerWakeReason(filtered[1], PlannerWakeReason.ExplicitHumanWake), string.Join(' ', filtered.Skip(2))),
            "sleep" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: planner sleep <reason> <detail...>")
                : services.OperatorSurfaceService.PlannerSleep(ParsePlannerSleepReason(filtered[1], PlannerSleepReason.WaitingForHumanAction), string.Join(' ', filtered.Skip(2))),
            _ => OperatorCommandResult.Failure("Usage: planner <status|run|host|wake|sleep> [...]"),
        };
    }

    private static OperatorCommandResult RunExplainTask(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return arguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: explain-task <task-id>")
            : services.OperatorSurfaceService.ExplainTask(arguments[0]);
    }
}
