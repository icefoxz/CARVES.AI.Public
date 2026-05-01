using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

public static partial class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var commandLine = CommandLineArguments.Parse(args);
            var hostResult = TryHandleHostBoundary(commandLine, out var hostFallbackNotice);
            if (hostResult is not null)
            {
                WriteLines(hostResult.Lines, hostResult.ExitCode == 0);
                return hostResult.ExitCode;
            }

            var services = RuntimeComposition.Create(commandLine.RepoRoot);
            var result = Dispatch(services, commandLine);
            if (!string.IsNullOrWhiteSpace(hostFallbackNotice))
            {
                result = new OperatorCommandResult(result.ExitCode, [hostFallbackNotice, .. result.Lines]);
            }

            WriteLines(result.Lines, result.ExitCode == 0);
            return result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static OperatorCommandResult Dispatch(RuntimeServices services, CommandLineArguments commandLine)
    {
        return commandLine.Command switch
        {
            "discover" => FormatHostDiscovery(commandLine.RepoRoot, new LocalHostClient(commandLine.RepoRoot).Discover()),
            "status" => RunStatusCommand(services, commandLine.Arguments),
            "audit" => RunAuditCommand(services, commandLine.Arguments),
            "verify" => RunVerifyCommand(services, commandLine.Arguments),
            "reconcile" => RunReconcileCommand(services, commandLine.Arguments),
            "repair" => LocalHostCommandDispatcher.Dispatch(services, "repair", commandLine.Arguments),
            "rebuild" => LocalHostCommandDispatcher.Dispatch(services, "rebuild", commandLine.Arguments),
            "reset" => LocalHostCommandDispatcher.Dispatch(services, "reset", commandLine.Arguments),
            "compact-history" => LocalHostCommandDispatcher.Dispatch(services, "compact-history", commandLine.Arguments),
            "cleanup" => RunCleanupCommand(services, commandLine.Arguments),
            "hosts" => services.OperatorSurfaceService.Hosts(),
            "fleet" => RunFleetCommand(services, commandLine.Arguments),
            "plan" => LocalHostCommandDispatcher.Dispatch(services, "plan", commandLine.Arguments),
            "plan-card" => RunPlanCard(services, commandLine.Arguments),
            "approve-task" => RunApproveTask(services, commandLine.Arguments),
            "approve-review" => RunApproveReview(services, commandLine.Arguments),
            "reject-review" => RunRejectReview(services, commandLine.Arguments),
            "reopen-review" => RunReopenReview(services, commandLine.Arguments),
            "run" => RunRunCommand(services, commandLine),
            "host" or "gateway" => RunHostCommand(services, commandLine),
            "console" => RunOperatorConsole(services, commandLine.Arguments),
            "attach" => RunAttachRepo(services, commandLine.Arguments),
            "create-card-draft" => LocalHostCommandDispatcher.Dispatch(services, "create-card-draft", commandLine.Arguments),
            "update-card" => LocalHostCommandDispatcher.Dispatch(services, "update-card", commandLine.Arguments),
            "list-cards" => LocalHostCommandDispatcher.Dispatch(services, "list-cards", commandLine.Arguments),
            "inspect-card" => LocalHostCommandDispatcher.Dispatch(services, "inspect-card", commandLine.Arguments),
            "approve-card" => LocalHostCommandDispatcher.Dispatch(services, "approve-card", commandLine.Arguments),
            "reject-card" => LocalHostCommandDispatcher.Dispatch(services, "reject-card", commandLine.Arguments),
            "archive-card" => LocalHostCommandDispatcher.Dispatch(services, "archive-card", commandLine.Arguments),
            "supersede-card-tasks" => LocalHostCommandDispatcher.Dispatch(services, "supersede-card-tasks", commandLine.Arguments),
            "create-taskgraph-draft" => LocalHostCommandDispatcher.Dispatch(services, "create-taskgraph-draft", commandLine.Arguments),
            "approve-taskgraph-draft" => LocalHostCommandDispatcher.Dispatch(services, "approve-taskgraph-draft", commandLine.Arguments),
            "approve-suggested-task" => LocalHostCommandDispatcher.Dispatch(services, "approve-suggested-task", commandLine.Arguments),
            "run-next" => services.OperatorSurfaceService.RunNext(commandLine.Arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))),
            "review-task" => RunReviewTask(services, commandLine.Arguments),
            "session" => RunSessionCommand(services, commandLine.Arguments),
            "planner" => RunPlannerCommand(services, commandLine.Arguments),
            "sync-state" => services.OperatorSurfaceService.SyncState(),
            "scan-code" => services.OperatorSurfaceService.ScanCode(),
            "safety-check" => services.OperatorSurfaceService.SafetyCheck(),
            "detect-refactors" => services.OperatorSurfaceService.DetectRefactors(),
            "materialize-refactors" => services.OperatorSurfaceService.MaterializeRefactors(),
            "detect-opportunities" => services.OperatorSurfaceService.DetectOpportunities(),
            "show-opportunities" => services.OperatorSurfaceService.ShowOpportunities(),
            "show-graph" => services.OperatorSurfaceService.ShowGraph(),
            "show-backlog" => services.OperatorSurfaceService.ShowBacklog(),
            "repo" => RunRepoCommand(services, commandLine.Arguments),
            "runtime" => RunRuntimeCommand(services, commandLine.Arguments),
            "pack" => RunPackCommand(services, commandLine.Arguments),
            "provider" => RunProviderCommand(services, commandLine.Arguments),
            "policy" => LocalHostCommandDispatcher.Dispatch(services, "policy", commandLine.Arguments),
            "report" => RunReportCommand(services, commandLine.Arguments),
            "failures" => RunFailuresCommand(services, commandLine.Arguments),
            "governance" => RunGovernanceCommand(services, commandLine.Arguments),
            "worker" => RunWorkerCommand(services, commandLine.Arguments),
            "qualification" => RunQualificationCommand(services, commandLine.Arguments),
            "validation" => RunValidationCommand(services, commandLine.Arguments),
            "validate" => RunValidateCommand(services, commandLine.Arguments),
            "actor" => LocalHostCommandDispatcher.Dispatch(services, "actor", commandLine.Arguments),
            "api" => RunApiCommand(services, commandLine.Arguments),
            "dashboard" => RunDashboardCommand(services, commandLine.Arguments),
            "workbench" => RunWorkbenchCommand(services, commandLine.Arguments),
            "intent" => LocalHostCommandDispatcher.Dispatch(services, "intent", commandLine.Arguments),
            "protocol" => LocalHostCommandDispatcher.Dispatch(services, "protocol", commandLine.Arguments),
            "prompt" => LocalHostCommandDispatcher.Dispatch(services, "prompt", commandLine.Arguments),
            "pilot" => LocalHostCommandDispatcher.Dispatch(services, "pilot", commandLine.Arguments),
            "context" => LocalHostCommandDispatcher.Dispatch(services, "context", commandLine.Arguments),
            "evidence" => LocalHostCommandDispatcher.Dispatch(services, "evidence", commandLine.Arguments),
            "memory" => LocalHostCommandDispatcher.Dispatch(services, "memory", commandLine.Arguments),
            "lint-platform" => services.OperatorSurfaceService.LintPlatform(),
            "explain-task" => RunExplainTask(services, commandLine.Arguments),
            "inspect" => RunInspectCommand(services, commandLine.Arguments),
            "card" => LocalHostCommandDispatcher.Dispatch(services, "card", commandLine.Arguments),
            "task" => LocalHostCommandDispatcher.Dispatch(services, "task", commandLine.Arguments),
            "discuss" => LocalHostCommandDispatcher.Dispatch(services, "discuss", commandLine.Arguments),
            "agent" => RunAgentCommand(commandLine, services),
            "help" => services.OperatorSurfaceService.Help(),
            _ => OperatorCommandResult.Failure("Unknown command.", string.Empty)
        };
    }

    private static OperatorCommandResult RunRunCommand(RuntimeServices services, CommandLineArguments commandLine)
    {
        if (commandLine.Arguments.Count > 0 && string.Equals(commandLine.Arguments[0], "task", StringComparison.OrdinalIgnoreCase))
        {
            return LocalHostCommandDispatcher.Dispatch(services, "task", ["run", .. commandLine.Arguments.Skip(1)]);
        }

        return RunExternalRepo(commandLine);
    }

    private static void WriteLines(IEnumerable<string> lines, bool stdout)
    {
        var writer = stdout ? Console.Out : Console.Error;
        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }
    }
}
