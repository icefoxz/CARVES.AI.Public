using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static OperatorCommandResult RunExternalRepo(CommandLineArguments commandLine)
    {
        if (commandLine.Arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: run <repo-path> [--dry-run]");
        }

        var dryRun = commandLine.Arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var repoArgument = commandLine.Arguments.First(argument => !string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var targetRoot = ResolvePath(commandLine.RepoRoot, repoArgument);
        var targetPaths = ControlPlanePaths.FromRepoRoot(targetRoot);
        var configRepository = new FileControlPlaneConfigRepository(targetPaths);
        var binding = RuntimeTargetBinding.Create(commandLine.RepoRoot, targetRoot, configRepository.LoadSystemConfig());
        var targetServices = RuntimeComposition.Create(binding.TargetRoot);
        var result = targetServices.OperatorSurfaceService.RunNext(dryRun);

        var lines = new List<string>
        {
            $"Runtime root: {binding.RuntimeRoot}",
            $"Target repo: {binding.TargetRoot}",
            $"Target AI root: {binding.TargetPaths.AiRoot}",
            $"Worktree root: {binding.WorktreeRoot}",
        };
        lines.AddRange(result.Lines);
        return new OperatorCommandResult(result.ExitCode, lines);
    }

    private static OperatorCommandResult RunHostCommand(RuntimeServices services, CommandLineArguments commandLine)
    {
        var effectiveCommandLine = commandLine with
        {
            Arguments = ResolveGatewayDefaultServeArguments(commandLine),
        };
        var arguments = effectiveCommandLine.Arguments;

        if (arguments.Count > 0 && string.Equals(arguments[0], "serve", StringComparison.OrdinalIgnoreCase))
        {
            LocalHostStartupLock? startupLock = null;
            if (!IsBackgroundGatewayServeLaunch())
            {
                var preflight = TryPrepareForegroundGatewayServe(effectiveCommandLine, out startupLock);
                if (preflight is not null)
                {
                    return preflight;
                }
            }

            var port = ResolveOptionalPositiveInt(arguments, "--port", FindFreePort());
            var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", 500, allowZero: true);
            try
            {
                var server = new LocalHostServer(services, port, intervalMilliseconds);
                server.Run();
                if (IsBackgroundGatewayServeLaunch())
                {
                    return new OperatorCommandResult(0, Array.Empty<string>());
                }

                return OperatorCommandResult.Success("Resident host exited cleanly.");
            }
            finally
            {
                startupLock?.Dispose();
            }
        }

        var remaining = arguments.Count > 0 && !arguments[0].StartsWith("--", StringComparison.Ordinal)
            ? arguments.Skip(1).ToArray()
            : arguments.ToArray();
        return RunResidentHost(services, remaining);
    }

    private static IReadOnlyList<string> ResolveGatewayDefaultServeArguments(CommandLineArguments commandLine)
    {
        if (!string.Equals(commandLine.Command, "gateway", StringComparison.OrdinalIgnoreCase))
        {
            return commandLine.Arguments;
        }

        if (commandLine.Arguments.Count == 0)
        {
            return ["serve"];
        }

        return commandLine.Arguments[0].StartsWith("--", StringComparison.Ordinal)
            ? ["serve", .. commandLine.Arguments]
            : commandLine.Arguments;
    }

    private static bool IsBackgroundGatewayServeLaunch()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("CARVES_HOST_SERVE_SUPPRESS_EXIT_OUTPUT"),
            "1",
            StringComparison.Ordinal);
    }

    private static OperatorCommandResult? TryPrepareForegroundGatewayServe(
        CommandLineArguments commandLine,
        out LocalHostStartupLock? startupLock)
    {
        startupLock = null;
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var client = new LocalHostClient(commandLine.RepoRoot);
        var existing = client.Discover();
        if (existing.HostRunning && existing.Summary is not null)
        {
            return RenderGatewayServeAlreadyRunning(commandLine.RepoRoot, existing, wantsJson);
        }

        if (HasConflictingHostSession(existing))
        {
            return RenderGatewayServeConflict(commandLine.RepoRoot, existing, wantsJson);
        }

        startupLock = LocalHostStartupLock.TryAcquire(commandLine.RepoRoot);
        if (startupLock is null)
        {
            var current = client.Discover();
            if (current.HostRunning && current.Summary is not null)
            {
                return RenderGatewayServeAlreadyRunning(commandLine.RepoRoot, current, wantsJson);
            }

            if (HasConflictingHostSession(current))
            {
                return RenderGatewayServeConflict(commandLine.RepoRoot, current, wantsJson);
            }

            return RenderHostStartupInProgress(commandLine.RepoRoot, wantsJson);
        }

        existing = client.Discover();
        if (existing.HostRunning && existing.Summary is not null)
        {
            startupLock.Dispose();
            startupLock = null;
            return RenderGatewayServeAlreadyRunning(commandLine.RepoRoot, existing, wantsJson);
        }

        if (HasConflictingHostSession(existing))
        {
            startupLock.Dispose();
            startupLock = null;
            return RenderGatewayServeConflict(commandLine.RepoRoot, existing, wantsJson);
        }

        return null;
    }

    private static OperatorCommandResult RenderGatewayServeAlreadyRunning(string repoRoot, HostDiscoveryResult discovery, bool wantsJson)
    {
        const string nextAction = "carves gateway status --json";
        var processId = discovery.Descriptor?.ProcessId ?? discovery.Snapshot?.ProcessId;
        var baseUrl = discovery.Summary?.BaseUrl ?? discovery.Descriptor?.BaseUrl ?? discovery.Snapshot?.BaseUrl ?? string.Empty;
        var message = "Foreground gateway serve refused because a resident gateway is already running for this repo.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-serve.v1",
                RepoRoot = repoRoot,
                GatewayReady = true,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ForegroundServeStarted = false,
                AlreadyRunning = true,
                ConflictPresent = false,
                ExistingProcessId = processId,
                BaseUrl = baseUrl,
                DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
                Message = message,
                NextAction = nextAction,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        return new OperatorCommandResult(1, [
            "CARVES gateway serve",
            $"Repo root: {repoRoot}",
            "Foreground serve started: False",
            "Gateway ready: True",
            "Already running: True",
            $"Existing process id: {processId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
            $"Base URL: {baseUrl}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            message,
            $"Next action: {nextAction}",
        ]);
    }

    private static OperatorCommandResult RenderGatewayServeConflict(string repoRoot, HostDiscoveryResult discovery, bool wantsJson)
    {
        var descriptor = discovery.Descriptor
            ?? throw new InvalidOperationException("Expected a resident host descriptor for gateway serve conflict rendering.");
        const string nextAction = "carves gateway reconcile --replace-stale --json";
        const string nextActionKind = LocalHostRecommendedActions.ReconcileStaleHost;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness("host_session_conflict", ready: false, nextActionKind, nextAction);
        var message = "Foreground gateway serve refused because a stale conflicting host generation is still present.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-serve.v1",
                RepoRoot = repoRoot,
                GatewayReady = false,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ForegroundServeStarted = false,
                AlreadyRunning = false,
                ConflictPresent = true,
                ConflictingProcessId = descriptor.ProcessId,
                ConflictingBaseUrl = descriptor.BaseUrl,
                Message = message,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        return new OperatorCommandResult(1, [
            "CARVES gateway serve",
            $"Repo root: {repoRoot}",
            "Foreground serve started: False",
            "Gateway ready: False",
            "Already running: False",
            "Conflict present: True",
            $"Conflicting process id: {descriptor.ProcessId}",
            $"Conflicting base URL: {descriptor.BaseUrl}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            message,
            $"Next action: {nextAction}",
            $"Next action kind: {nextActionKind}",
            $"Lifecycle state: {lifecycle.State}",
            $"Lifecycle reason: {lifecycle.Reason}",
        ]);
    }

    private static OperatorCommandResult RunResidentHost(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var cycles = ResolveOptionalPositiveInt(arguments, "--cycles", defaultValue: 0);
        var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", defaultValue: 250, allowZero: true);
        var slots = ResolveOptionalPositiveInt(arguments, "--slots", defaultValue: 1);
        return new ResidentRuntimeHostService(services).Run(dryRun, cycles, intervalMilliseconds, slots);
    }

    private static OperatorCommandResult RunOperatorConsole(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return new InteractiveOperatorConsoleService(services).Run(ResolveOption(arguments, "--script"));
    }

    private static OperatorCommandResult RunAttachRepo(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var startRuntime = arguments.Any(argument => string.Equals(argument, "--start-runtime", StringComparison.OrdinalIgnoreCase));
        var force = arguments.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));
        var repoPath = ResolvePrimaryArgument(arguments, ["--repo-id", "--provider-profile", "--policy-profile", "--client-repo-root"], ["--dry-run", "--start-runtime", "--force"])
            ?? services.Paths.RepoRoot;

        return new TargetRepoAttachService(services).Attach(
            ResolvePath(services.Paths.RepoRoot, repoPath),
            ResolveOption(arguments, "--repo-id"),
            ResolveOption(arguments, "--provider-profile"),
            ResolveOption(arguments, "--policy-profile"),
            startRuntime,
            dryRun,
            force,
            ResolveOption(arguments, "--client-repo-root"));
    }
}
