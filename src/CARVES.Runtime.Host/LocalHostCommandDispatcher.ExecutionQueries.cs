using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunInspectCommandCore(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var inspectUsage = BuildInspectUsage();
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure(inspectUsage);
        }

        if (arguments.Count < 2 && FixedInspectCommandRequiresArgument(arguments[0]))
        {
            return OperatorCommandResult.Failure(inspectUsage);
        }

        if (string.Equals(arguments[0], "all-surfaces", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Success(BuildInspectAllSurfacesUsage());
        }

        var surface = new LocalHostSurfaceService(services);
        var result = arguments[0].ToLowerInvariant() switch
        {
            "methodology" => services.OperatorSurfaceService.InspectMethodologyGate(arguments[1]),
            "async-resume-gate" => services.OperatorSurfaceService.InspectAsyncResumeGate(),
            "card" => RunCardCommand(services, ["inspect", arguments[1]]),
            "card-draft" => services.OperatorSurfaceService.InspectCardDraft(arguments[1]),
            "task" => RunTaskCommand(services, ["inspect", arguments[1], .. arguments.Skip(2)]),
            "taskgraph-draft" => services.OperatorSurfaceService.InspectTaskGraphDraft(arguments[1]),
            "run" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildRunInspect(arguments[1]))),
            "dispatch" => services.OperatorSurfaceService.InspectDispatch(),
            "worker-automation-readiness" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerAutomationReadiness(
                ResolveOption(arguments, "--repo-id") ?? (arguments.Count >= 2 ? arguments[1] : null),
                arguments.Any(argument => string.Equals(argument, "--refresh-worker-health", StringComparison.OrdinalIgnoreCase))))),
            "worker-automation-schedule-tick" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerAutomationScheduleTick(
                ResolveOption(arguments, "--repo-id") ?? (arguments.Count >= 2 ? arguments[1] : null),
                arguments.Any(argument => string.Equals(argument, "--refresh-worker-health", StringComparison.OrdinalIgnoreCase)),
                dispatchRequested: false,
                dryRun: true))),
            "routing-profile" => services.OperatorSurfaceService.InspectRoutingProfile(),
            "qualification" => services.OperatorSurfaceService.InspectQualification(),
            "qualification-candidate" => services.OperatorSurfaceService.InspectQualificationCandidate(),
            "qualification-promotion" => services.OperatorSurfaceService.InspectQualificationPromotionDecision(arguments.Count >= 2 ? arguments[1] : null),
            "promotion-readiness" => services.OperatorSurfaceService.InspectPromotionReadiness(arguments.Count >= 2 ? arguments[1] : null),
            "validation-suite" => services.OperatorSurfaceService.InspectValidationSuite(),
            "validation-trace" => services.OperatorSurfaceService.InspectValidationTrace(arguments[1]),
            "validation-summary" => services.OperatorSurfaceService.InspectValidationSummary(arguments.Count >= 2 ? arguments[1] : null),
            "validation-history" => services.OperatorSurfaceService.InspectValidationHistory(ResolveOptionalInt(arguments, "--limit")),
            "validation-coverage" => services.OperatorSurfaceService.InspectValidationCoverage(arguments.Count >= 2 ? arguments[1] : null),
            "sustainability" => services.OperatorSurfaceService.InspectSustainability(),
            "history-compaction" => services.OperatorSurfaceService.InspectHistoryCompaction(),
            "archive-readiness" => services.OperatorSurfaceService.InspectArchiveReadiness(),
            "archive-followup" => services.OperatorSurfaceService.InspectArchiveFollowUp(),
            "execution-run-exceptions" => services.OperatorSurfaceService.InspectExecutionRunExceptions(),
            "context-pack" => services.OperatorSurfaceService.InspectContextPack(arguments[1]),
            "execution-budget" => services.OperatorSurfaceService.InspectExecutionBudget(arguments[1]),
            "execution-risk" => services.OperatorSurfaceService.InspectExecutionRisk(arguments[1]),
            "boundary" => services.OperatorSurfaceService.InspectBoundary(arguments[1]),
            "execution-pattern" => services.OperatorSurfaceService.InspectExecutionPattern(arguments[1]),
            "execution-trace" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildExecutionTraceInspect(arguments[1]))),
            "worker-dispatch-pilot-evidence" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerDispatchPilotEvidence(arguments[1]))),
            "replan" => services.OperatorSurfaceService.InspectReplan(arguments[1]),
            "suggested-tasks" => services.OperatorSurfaceService.InspectSuggestedTasks(arguments[1]),
            "execution-memory" => services.OperatorSurfaceService.InspectExecutionMemory(arguments[1]),
            "attach-proof" => services.OperatorSurfaceService.InspectAttachProof(arguments[1]),
            "pilot-evidence" => services.OperatorSurfaceService.InspectPilotEvidence(arguments[1]),
            _ => RuntimeSurfaceCommandRegistry.TryDispatchInspect(services.OperatorSurfaceService, arguments, out var registryResult)
                ? registryResult
                : OperatorCommandResult.Failure(inspectUsage),
        };

        return string.Equals(arguments[0], "runtime-product-closure-pilot-status", StringComparison.OrdinalIgnoreCase)
            ? DecoratePilotStatusText(services, result)
            : result;
    }

    private static OperatorCommandResult RunTaskCommandCore(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: task <inspect|run|ingest-result> <task-id> [--dry-run] [--runs]");
        }

        if (arguments.Any(IsTaskHelpArgument))
        {
            return RenderDispatcherTaskHelp(arguments[0]);
        }

        var surface = new LocalHostSurfaceService(services);
        if (string.Equals(arguments[0], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2)
            {
                return OperatorCommandResult.Failure("Usage: task inspect <task-id> [--runs]");
            }

            return OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildTaskInspect(arguments[1], arguments.Any(argument => string.Equals(argument, "--runs", StringComparison.OrdinalIgnoreCase)))));
        }

        if (string.Equals(arguments[0], "run", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2)
            {
                return OperatorCommandResult.Failure("Usage: task run <task-id> [--dry-run] [--manual-fallback] [--force-fallback] [--routing-intent <intent>] [--routing-module <module>]");
            }

            var taskId = arguments[1];
            var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
            var manualFallback = arguments.Any(argument => string.Equals(argument, "--manual-fallback", StringComparison.OrdinalIgnoreCase));
            var forceFallbackOnly = arguments.Any(argument => string.Equals(argument, "--force-fallback", StringComparison.OrdinalIgnoreCase));
            var routingIntentOverride = ResolveOption(arguments, "--routing-intent");
            var routingModuleOverride = ResolveOption(arguments, "--routing-module");
            var selectionOptions = BuildWorkerSelectionOptions(routingIntentOverride, routingModuleOverride, forceFallbackOnly);
            var result = services.OperatorSurfaceService.RunDelegatedTask(taskId, dryRun, ActorSessionKind.Operator, "operator", manualFallback, selectionOptions);
            return new OperatorCommandResult(result.Accepted ? 0 : 1, [surface.ToPrettyJson(result)]);
        }

        if (string.Equals(arguments[0], "ingest-result", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2)
            {
                return OperatorCommandResult.Failure("Usage: task ingest-result <task-id>");
            }

            return services.OperatorSurfaceService.IngestTaskResult(arguments[1]);
        }

        if (string.Equals(arguments[0], "retry", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 3)
            {
                return OperatorCommandResult.Failure("Usage: task retry <task-id> <reason...>");
            }

            return services.OperatorSurfaceService.RetryTask(arguments[1], string.Join(' ', arguments.Skip(2)));
        }

        return OperatorCommandResult.Failure("Usage: task <inspect|run|ingest-result|retry> <task-id> [--dry-run] [--runs]");
    }

    private static bool IsTaskHelpArgument(string argument)
    {
        return string.Equals(argument, "help", StringComparison.OrdinalIgnoreCase)
               || string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
               || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static OperatorCommandResult RenderDispatcherTaskHelp(string? subCommand)
    {
        var normalized = subCommand?.ToLowerInvariant();
        var usage = normalized switch
        {
            "inspect" => "Usage: task inspect <task-id> [--runs]",
            "run" => "Usage: task run <task-id> [--dry-run] [--manual-fallback] [--force-fallback] [--routing-intent <intent>] [--routing-module <module>]",
            "ingest-result" => "Usage: task ingest-result <task-id>",
            "retry" => "Usage: task retry <task-id> <reason...>",
            _ => "Usage: task <inspect|run|ingest-result|retry> <task-id> [--dry-run] [--runs]",
        };

        return OperatorCommandResult.Success(
            usage,
            "       Help requests are read-only and never start a delegated task run.");
    }

    private static OperatorCommandResult RunDiscussCommandCore(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: discuss <context|brief-preview|planner|blocked|card|task> [...]");
        }

        var surface = new LocalHostSurfaceService(services);
        JsonNode? node = arguments[0].ToLowerInvariant() switch
        {
            "context" => surface.BuildDiscussionContext(),
            "brief-preview" => surface.BuildDiscussionBriefPreview(),
            "planner" => surface.BuildDiscussionPlanner(),
            "blocked" => surface.BuildDiscussionBlocked(),
            "card" when arguments.Count >= 2 => surface.BuildDiscussionCard(arguments[1]),
            "task" when arguments.Count >= 2 => surface.BuildDiscussionTask(arguments[1]),
            _ => null,
        };
        if (node is null)
        {
            return OperatorCommandResult.Failure("Usage: discuss <context|brief-preview|planner|blocked|card|task> [...]");
        }

        return OperatorCommandResult.Success(surface.ToPrettyJson(node));
    }
}
