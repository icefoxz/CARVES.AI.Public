using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static readonly TimeSpan HostStartReadinessTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GatewayActivityMaintenanceFreshnessThreshold = TimeSpan.FromDays(GatewayActivityJournal.DefaultArchiveBeforeDays);
    private static readonly TimeSpan GatewayActivityVerificationFreshnessThreshold = TimeSpan.FromHours(24);
    private const string HostEnsureFirstCommand = "carves host ensure --json";
    private const string HostReconcileStaleCommand = "carves host reconcile --replace-stale --json";

    private static OperatorCommandResult? TryHandleHostBoundary(CommandLineArguments commandLine, out string? hostFallbackNotice)
    {
        hostFallbackNotice = null;
        if (string.Equals(commandLine.Command, "discover", StringComparison.OrdinalIgnoreCase))
        {
            return FormatHostDiscovery(commandLine.RepoRoot, new LocalHostClient(commandLine.RepoRoot).Discover());
        }

        if (IsHostGatewayLifecycleCommand(commandLine.Command) && commandLine.Arguments.Count > 0)
        {
            if (IsHostHelpRequest(commandLine.Arguments))
            {
                return RenderHostHelp(commandLine.Command);
            }

            return commandLine.Arguments[0].ToLowerInvariant() switch
            {
                "start" => StartHost(commandLine),
                "ensure" => EnsureHost(commandLine),
                "reconcile" => ReconcileHost(commandLine),
                "restart" => RestartHost(commandLine),
                "status" => HostStatus(commandLine),
                "doctor" => GatewayDoctor(commandLine),
                "logs" => GatewayLogs(commandLine),
                "activity" => IsGatewayActivityMaintenance(commandLine.Arguments)
                    ? GatewayActivityMaintenance(commandLine)
                    : IsGatewayActivityStatus(commandLine.Arguments)
                        ? GatewayActivityStatus(commandLine)
                        : IsGatewayActivityVerify(commandLine.Arguments)
                            ? GatewayActivityVerify(commandLine)
                            : IsGatewayActivityArchive(commandLine.Arguments)
                                ? GatewayActivityArchive(commandLine)
                                : IsGatewayActivityExplain(commandLine.Arguments)
                                    ? GatewayActivityExplain(commandLine)
                                    : GatewayActivity(commandLine),
                "pause" => PauseHost(commandLine),
                "resume" => ResumeHost(commandLine),
                "stop" => StopHost(commandLine),
                _ => null,
            };
        }

        if (TryRenderTaskHelp(commandLine, out var taskHelp))
        {
            return taskHelp;
        }

        if (commandLine.UseColdPath)
        {
            return null;
        }

        var minimumStatefulActionFamily = ResolveMinimumStatefulActionFamily(commandLine.Command, commandLine.Arguments);
        var minimumStatefulActionCapability = ResolveRequiredCapabilityForMinimumStatefulActionFamily(minimumStatefulActionFamily);
        if (minimumStatefulActionCapability is not null
            && !new LocalHostClient(commandLine.RepoRoot).Discover(minimumStatefulActionCapability).HostRunning)
        {
            return string.Equals(minimumStatefulActionCapability, "delegated-execution", StringComparison.OrdinalIgnoreCase)
                ? OperatorCommandResult.Failure(
                    "Delegated execution requires a resident host.",
                    $"Run `{HostEnsureFirstCommand}` before `task run`; use `--cold task run <task-id>` only as an explicit manual fallback.")
                : OperatorCommandResult.Failure(
                    "Resident host is required for this stateful action family.",
                    $"Run `{HostEnsureFirstCommand}` before `{commandLine.Command}`; use the explicit `--cold` fallback only when the manual override is approved.");
        }

        var requiredCapability = ResolveHostCapability(commandLine.Command, commandLine.Arguments);
        if (requiredCapability is null)
        {
            return null;
        }

        var client = new LocalHostClient(commandLine.RepoRoot);
        var discovery = client.Discover(requiredCapability);
        if (!discovery.HostRunning)
        {
            if (IsControlPlaneMutationCommand(commandLine.Command, commandLine.Arguments))
            {
                return OperatorCommandResult.Failure(
                    discovery.Message,
                    $"Run `{HostEnsureFirstCommand}` before planner/review mutations; use the explicit `--cold` fallback only when the manual override is approved.");
            }

            if (string.Equals(commandLine.Command, "agent", StringComparison.OrdinalIgnoreCase))
            {
                return OperatorCommandResult.Failure(discovery.Message, $"Run `{HostEnsureFirstCommand}` before calling the agent gateway.");
            }

            if (ShouldEmitHostFallbackNotice(commandLine.Command))
            {
                hostFallbackNotice = BuildHostFallbackNotice(commandLine.Command, discovery);
            }

            return null;
        }

        if (string.Equals(commandLine.Command, "console", StringComparison.OrdinalIgnoreCase))
        {
            return new InteractiveOperatorConsoleService(client).Run(ResolveOption(commandLine.Arguments, "--script"));
        }

        if (string.Equals(commandLine.Command, "agent", StringComparison.OrdinalIgnoreCase))
        {
            return RunAgentCommand(commandLine, services: null);
        }

        if (string.Equals(commandLine.Command, "dashboard", StringComparison.OrdinalIgnoreCase))
        {
            if (commandLine.Arguments.Any(argument => string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase)))
            {
                return client.Invoke(commandLine.Command, commandLine.Arguments, requiredCapability);
            }

            var summary = discovery.Summary ?? throw new InvalidOperationException("Host discovery returned no summary.");
            return OperatorCommandResult.Success(
                $"Dashboard: {summary.DashboardUrl}",
                "Use `dashboard --text` for the line-based summary.");
        }

        if (string.Equals(commandLine.Command, "workbench", StringComparison.OrdinalIgnoreCase))
        {
            if (commandLine.Arguments.Any(argument => string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase)))
            {
                return client.Invoke(commandLine.Command, commandLine.Arguments, requiredCapability);
            }

            var summary = discovery.Summary ?? throw new InvalidOperationException("Host discovery returned no summary.");
            return OperatorCommandResult.Success(
                $"Workbench: {ResolveWorkbenchUrl(summary.BaseUrl, commandLine.Arguments)}",
                "Use `workbench --text` for the line-based workbench.");
        }

        return client.Invoke(commandLine.Command, commandLine.Arguments, requiredCapability);
    }

    private static bool IsHostHelpRequest(IReadOnlyList<string> arguments)
    {
        return arguments.Any(static argument => argument is "help" or "--help" or "-h");
    }

    private static bool IsHostGatewayLifecycleCommand(string command)
    {
        return string.Equals(command, "host", StringComparison.OrdinalIgnoreCase)
               || string.Equals(command, "gateway", StringComparison.OrdinalIgnoreCase);
    }

    private static OperatorCommandResult RenderHostHelp(string command)
    {
        var entry = string.Equals(command, "gateway", StringComparison.OrdinalIgnoreCase)
            ? "gateway"
            : "host";
        return OperatorCommandResult.Success(
            $"Usage: carves {entry} <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>",
            "       Gateway role: resident connection, routing, and observability for CARVES surfaces.",
            "       Gateway boundary: it does not dispatch worker automation; role-mode gates control automation separately.",
            $"       {HostEnsureFirstCommand}",
            entry == "gateway"
                ? "       carves gateway with no subcommand is the foreground gateway terminal."
                : "       host with no subcommand runs the resident runtime host loop.",
            $"       {entry} serve [--port <port>] [--interval-ms <milliseconds>]",
            $"       {entry} start [--port <port>] [--interval-ms <milliseconds>]",
            $"       {entry} ensure [--json] [--require-capability <capability>] [--interval-ms <milliseconds>]",
            $"       {entry} reconcile --replace-stale [--json]",
            $"       {entry} restart [--json] [--force] [--port <port>] [--interval-ms <milliseconds>] [reason...]",
            $"       {entry} status [--json] [--require-capability <capability>]",
            $"       {entry} doctor [--json] [--tail <lines>]",
            $"       {entry} logs [--json] [--tail <lines>]",
            $"       {entry} activity [--json] [--tail <lines>] [--since-minutes <minutes>] [--category <category>] [--event <event-kind>] [--command <command>] [--path <path>] [--request-id <id>] [--operation-id <id>] [--session-id <id>] [--message-id <id>] [--compact --retain <lines>]",
            $"       {entry} activity status [--json] [--require-maturity]",
            $"       {entry} activity maintenance [--json] [--before-days <days>]",
            $"       {entry} activity explain <request-id> [--json]",
            $"       {entry} activity verify [--json]",
            $"       {entry} activity archive [--json] [--before-days <days>]",
            $"       Recommended entry: run `{HostEnsureFirstCommand}` first; if it reports host_session_conflict, run `{HostReconcileStaleCommand}`.",
            "       Help requests are read-only and never start a resident host.");
    }

    private static bool TryRenderTaskHelp(CommandLineArguments commandLine, out OperatorCommandResult? result)
    {
        result = null;
        if (string.Equals(commandLine.Command, "task", StringComparison.OrdinalIgnoreCase)
            && commandLine.Arguments.Any(IsHelpArgument))
        {
            result = RenderTaskHelp(commandLine.Arguments.FirstOrDefault());
            return true;
        }

        if (string.Equals(commandLine.Command, "run", StringComparison.OrdinalIgnoreCase)
            && commandLine.Arguments.Count > 0
            && string.Equals(commandLine.Arguments[0], "task", StringComparison.OrdinalIgnoreCase)
            && commandLine.Arguments.Skip(1).Any(IsHelpArgument))
        {
            result = RenderTaskHelp("run");
            return true;
        }

        return false;
    }

    private static bool IsHelpArgument(string argument)
    {
        return string.Equals(argument, "help", StringComparison.OrdinalIgnoreCase)
               || string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
               || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static OperatorCommandResult RenderTaskHelp(string? subCommand)
    {
        var normalized = subCommand?.ToLowerInvariant();
        var usage = normalized switch
        {
            "inspect" => "Usage: carves task inspect <task-id> [--runs]",
            "run" => "Usage: carves task run <task-id> [--dry-run] [--manual-fallback] [--force-fallback] [--routing-intent <intent>] [--routing-module <module>]",
            "ingest-result" => "Usage: carves task ingest-result <task-id>",
            "retry" => "Usage: carves task retry <task-id> <reason...>",
            _ => "Usage: carves task <inspect|run|ingest-result|retry> <task-id> [--dry-run] [--runs]",
        };

        return OperatorCommandResult.Success(
            usage,
            "       Help requests are read-only and never start a delegated task run.");
    }

    private static OperatorCommandResult StartHost(CommandLineArguments commandLine)
    {
        var client = new LocalHostClient(commandLine.RepoRoot);
        var existing = client.Discover();
        if (existing.HostRunning && existing.Summary is not null)
        {
            return FormatHostDiscovery(commandLine.RepoRoot, existing);
        }

        if (HasConflictingHostSession(existing))
        {
            return RenderHostSessionConflict(commandLine.RepoRoot, existing, wantsJson: false);
        }

        using var startupLock = LocalHostStartupLock.TryAcquire(commandLine.RepoRoot);
        if (startupLock is null)
        {
            var current = client.Discover();
            if (current.HostRunning && current.Summary is not null)
            {
                return FormatHostDiscovery(commandLine.RepoRoot, current);
            }

            if (HasConflictingHostSession(current))
            {
                return RenderHostSessionConflict(commandLine.RepoRoot, current, wantsJson: false);
            }

            return RenderHostStartupInProgress(commandLine.RepoRoot, wantsJson: false);
        }

        existing = client.Discover();
        if (existing.HostRunning && existing.Summary is not null)
        {
            return FormatHostDiscovery(commandLine.RepoRoot, existing);
        }

        if (HasConflictingHostSession(existing))
        {
            return RenderHostSessionConflict(commandLine.RepoRoot, existing, wantsJson: false);
        }

        var (processId, discovery) = StartFreshHostGeneration(commandLine.RepoRoot, commandLine.Arguments);
        if (discovery.HostRunning)
        {
            return FormatHostDiscovery(commandLine.RepoRoot, discovery, BuildStartPreface(processId, recoverySummary: null));
        }

        return OperatorCommandResult.Failure("Resident host did not become healthy after start.", $"Process id: {processId}");
    }

    private static string BuildStartPreface(int processId, string? recoverySummary)
    {
        return string.IsNullOrWhiteSpace(recoverySummary)
            ? $"Started resident host process {processId}."
            : $"{recoverySummary} Started resident host process {processId}.";
    }

    private static OperatorCommandResult EnsureHost(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var requiredCapability = ResolveOption(commandLine.Arguments, "--require-capability");
        var client = new LocalHostClient(commandLine.RepoRoot);
        var discovery = client.Discover();
        var started = false;
        string? startPreface = null;

        if (!discovery.HostRunning)
        {
            if (HasConflictingHostSession(discovery))
            {
                return RenderHostSessionConflict(commandLine.RepoRoot, discovery, wantsJson);
            }

            using var startupLock = LocalHostStartupLock.TryAcquire(commandLine.RepoRoot);
            if (startupLock is null)
            {
                var current = client.Discover();
                if (current.HostRunning && current.Summary is not null)
                {
                    discovery = current;
                }
                else
                {
                    if (HasConflictingHostSession(current))
                    {
                        return RenderHostSessionConflict(commandLine.RepoRoot, current, wantsJson);
                    }

                    return RenderHostStartupInProgress(commandLine.RepoRoot, wantsJson);
                }
            }
            else
            {
                discovery = client.Discover();
                if (!discovery.HostRunning)
                {
                    if (HasConflictingHostSession(discovery))
                    {
                        return RenderHostSessionConflict(commandLine.RepoRoot, discovery, wantsJson);
                    }

                    var (processId, startedDiscovery) = StartFreshHostGeneration(commandLine.RepoRoot, commandLine.Arguments);
                    started = true;
                    startPreface = BuildStartPreface(processId, recoverySummary: null);
                    discovery = startedDiscovery;
                }
            }
        }

        var compatible = true;
        if (discovery.HostRunning && !string.IsNullOrWhiteSpace(requiredCapability))
        {
            discovery = new LocalHostDiscoveryService().EnsureCompatible(commandLine.RepoRoot, [requiredCapability]);
            compatible = discovery.HostRunning;
        }

        return RenderHostEnsure(commandLine.RepoRoot, discovery, started, compatible, requiredCapability, wantsJson, startPreface);
    }

    private static OperatorCommandResult RenderHostEnsure(
        string repoRoot,
        HostDiscoveryResult discovery,
        bool started,
        bool compatible,
        string? requiredCapability,
        bool wantsJson,
        string? startPreface)
    {
        var ready = discovery.HostRunning && discovery.Summary is not null && compatible;
        var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        if (ready && !commandSurfaceCompatibility.Compatible)
        {
            ready = false;
        }

        var readiness = ready
            ? started ? "started" : "connected"
            : discovery.HostRunning && discovery.Summary is not null && !commandSurfaceCompatibility.Compatible
                ? "surface_registry_stale"
            : compatible ? "not_running" : "incompatible";
        var nextAction = ready
            ? "host ready"
            : discovery.HostRunning && discovery.Summary is not null && !commandSurfaceCompatibility.Compatible
                ? HostCommandSurfaceCatalog.RestartAction
            : string.IsNullOrWhiteSpace(requiredCapability)
                ? "carves host ensure --json"
                : $"carves host ensure --require-capability {requiredCapability} --json";
        var nextActionKind = ready
            ? LocalHostRecommendedActions.None
            : discovery.HostRunning && discovery.Summary is not null && !commandSurfaceCompatibility.Compatible
                ? LocalHostRecommendedActions.RestartForSurfaceRegistry
                : LocalHostRecommendedActions.EnsureHost;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness(readiness, ready, nextActionKind, nextAction);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-ensure.v1",
                RepoRoot = repoRoot,
                HostReadiness = readiness,
                HostRunning = discovery.HostRunning,
                Started = started,
                Compatible = ready,
                RequiredCapability = requiredCapability ?? string.Empty,
                Message = discovery.Message,
                BaseUrl = discovery.Summary?.BaseUrl ?? string.Empty,
                DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
                RuntimeDirectory = discovery.Summary?.RuntimeDirectory ?? string.Empty,
                DeploymentDirectory = discovery.Summary?.DeploymentDirectory ?? string.Empty,
                ProcessId = discovery.Descriptor?.ProcessId,
                Capabilities = discovery.Summary?.Capabilities ?? Array.Empty<string>(),
                HostCommandSurfaceCompatible = commandSurfaceCompatibility.Compatible,
                HostCommandSurfaceReadiness = commandSurfaceCompatibility.Readiness,
                HostCommandSurfaceReason = commandSurfaceCompatibility.Reason,
                ExpectedCommandSurfaceFingerprint = commandSurfaceCompatibility.ExpectedFingerprint,
                ActualCommandSurfaceFingerprint = commandSurfaceCompatibility.ActualFingerprint ?? string.Empty,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(ready ? 0 : 1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES host ensure",
            $"Repo root: {repoRoot}",
            $"Host readiness: {readiness}",
            $"Host running: {discovery.HostRunning}",
            $"Started: {started}",
            $"Compatible: {ready}",
        };
        if (!string.IsNullOrWhiteSpace(requiredCapability))
        {
            lines.Add($"Required capability: {requiredCapability}");
        }

        if (!string.IsNullOrWhiteSpace(startPreface))
        {
            lines.Add(startPreface);
        }

        lines.Add($"Message: {discovery.Message}");
        if (discovery.Summary is not null)
        {
            lines.Add($"Base URL: {discovery.Summary.BaseUrl}");
            lines.Add($"Dashboard: {discovery.Summary.DashboardUrl}");
            lines.Add($"Capabilities: {string.Join(", ", discovery.Summary.Capabilities)}");
            lines.Add($"Host command surface: {commandSurfaceCompatibility.Readiness}");
            lines.Add($"Host command surface reason: {commandSurfaceCompatibility.Reason}");
        }

        lines.Add($"Next action: {nextAction}");
        lines.Add($"Next action kind: {nextActionKind}");
        lines.Add($"Lifecycle state: {lifecycle.State}");
        lines.Add($"Lifecycle reason: {lifecycle.Reason}");
        return new OperatorCommandResult(ready ? 0 : 1, lines);
    }

    private static bool HasConflictingHostSession(HostDiscoveryResult discovery)
    {
        return !discovery.HostRunning
            && discovery.Summary is null
            && discovery.Descriptor is not null;
    }

    private static OperatorCommandResult RenderHostSessionConflict(string repoRoot, HostDiscoveryResult discovery, bool wantsJson)
    {
        var descriptor = discovery.Descriptor
            ?? throw new InvalidOperationException("Expected a resident host descriptor for conflict rendering.");
        var snapshot = discovery.Snapshot;
        var message = "Resident host session conflict detected. A prior host process still appears alive, but the active descriptor no longer resolves to a healthy host. Refusing to start another host generation automatically.";
        var nextAction = "carves host reconcile --replace-stale --json";
        const string nextActionKind = LocalHostRecommendedActions.ReconcileStaleHost;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness("host_session_conflict", ready: false, nextActionKind, nextAction);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-ensure.v1",
                RepoRoot = repoRoot,
                HostReadiness = "host_session_conflict",
                HostRunning = false,
                Started = false,
                Compatible = false,
                RequiredCapability = string.Empty,
                Message = message,
                ConflictPresent = true,
                ConflictingProcessId = descriptor.ProcessId,
                ConflictingBaseUrl = descriptor.BaseUrl,
                SnapshotState = snapshot?.State.ToString(),
                SnapshotRecordedAt = snapshot?.RecordedAt,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES host conflict",
            $"Repo root: {repoRoot}",
            "Host readiness: host_session_conflict",
            "Host running: False",
            "Started: False",
            "Compatible: False",
            $"Conflicting process id: {descriptor.ProcessId}",
            $"Conflicting base URL: {descriptor.BaseUrl}",
        };
        if (snapshot is not null)
        {
            lines.Add($"Host snapshot state: {snapshot.State}");
            lines.Add($"Host snapshot recorded at: {snapshot.RecordedAt:O}");
        }

        lines.Add(message);
        lines.Add($"Next action: {nextAction}");
        lines.Add($"Next action kind: {nextActionKind}");
        lines.Add($"Lifecycle state: {lifecycle.State}");
        lines.Add($"Lifecycle reason: {lifecycle.Reason}");
        return new OperatorCommandResult(1, lines);
    }

    private static OperatorCommandResult RenderHostStartupInProgress(string repoRoot, bool wantsJson)
    {
        var message = "A resident host startup attempt is already in progress for this repo. Refusing to start another host generation concurrently.";
        var nextAction = "carves host status --json";
        const string nextActionKind = LocalHostRecommendedActions.WaitForStartup;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness("host_start_in_progress", ready: false, nextActionKind, nextAction);
        var startupLockPath = LocalHostPaths.GetStartupLockPath(repoRoot);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-ensure.v1",
                RepoRoot = repoRoot,
                HostReadiness = "host_start_in_progress",
                HostRunning = false,
                Started = false,
                Compatible = false,
                RequiredCapability = string.Empty,
                Message = message,
                StartupLockPresent = true,
                StartupLockPath = startupLockPath,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        return new OperatorCommandResult(1, [
            "CARVES host startup guard",
            $"Repo root: {repoRoot}",
            "Host readiness: host_start_in_progress",
            "Host running: False",
            "Started: False",
            "Compatible: False",
            $"Startup lock path: {startupLockPath}",
            message,
            $"Next action: {nextAction}",
            $"Next action kind: {nextActionKind}",
            $"Lifecycle state: {lifecycle.State}",
            $"Lifecycle reason: {lifecycle.Reason}",
        ]);
    }

    private static OperatorCommandResult ReconcileHost(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var replaceStale = commandLine.Arguments.Any(argument => string.Equals(argument, "--replace-stale", StringComparison.OrdinalIgnoreCase));
        var client = new LocalHostClient(commandLine.RepoRoot);
        var discovery = client.Discover();

        if (discovery.HostRunning && discovery.Summary is not null)
        {
            return RenderHostReconcile(commandLine.RepoRoot, discovery, replaced: false, wantsJson, "Resident host is already healthy. No replacement was required.");
        }

        if (!replaceStale)
        {
            return RenderHostReconcileGuidance(commandLine.RepoRoot, discovery, wantsJson);
        }

        if (!HasConflictingHostSession(discovery))
        {
            return RenderHostReconcileGuidance(commandLine.RepoRoot, discovery, wantsJson, replaceRequested: true);
        }

        var descriptor = discovery.Descriptor!;
        var reason = $"Explicitly replaced stale resident host generation {descriptor.ProcessId}.";
        var stopped = LocalHostTerminator.ForceStop(commandLine.RepoRoot, descriptor, reason);
        if (!stopped)
        {
            return RenderHostReconcileFailure(
                commandLine.RepoRoot,
                wantsJson,
                message: "Failed to replace the stale resident host generation because the conflicting process could not be terminated.",
                conflictPresent: true,
                conflictingProcessId: descriptor.ProcessId,
                conflictingBaseUrl: descriptor.BaseUrl,
                nextAction: $"Manually terminate process {descriptor.ProcessId} and rerun `carves host reconcile --replace-stale --json`.",
                nextActionKind: LocalHostRecommendedActions.ManualTerminateConflictingProcess);
        }

        using var replacementStartupLock = LocalHostStartupLock.TryAcquire(commandLine.RepoRoot);
        if (replacementStartupLock is null)
        {
            return RenderHostStartupInProgress(commandLine.RepoRoot, wantsJson);
        }

        var (replacementProcessId, replacementDiscovery) = StartFreshHostGeneration(commandLine.RepoRoot, commandLine.Arguments);
        if (replacementDiscovery.HostRunning)
        {
            return RenderHostReconcile(
                commandLine.RepoRoot,
                replacementDiscovery,
                replaced: true,
                wantsJson,
                BuildStartPreface(replacementProcessId, $"Replaced stale resident host process {descriptor.ProcessId}."));
        }

        return RenderHostReconcileFailure(
            commandLine.RepoRoot,
            wantsJson,
            message: "Resident host did not become healthy after explicit stale host replacement.",
            conflictPresent: false,
            conflictingProcessId: replacementProcessId,
            conflictingBaseUrl: replacementDiscovery.Descriptor?.BaseUrl,
            nextAction: "carves host status --json",
            nextActionKind: LocalHostRecommendedActions.InspectHostStatus);
    }

    private static OperatorCommandResult RenderHostReconcileGuidance(string repoRoot, HostDiscoveryResult discovery, bool wantsJson, bool replaceRequested = false)
    {
        var conflictPresent = HasConflictingHostSession(discovery);
        var message = conflictPresent
            ? "A conflicting stale host generation exists. Safe ensure will not replace it automatically."
            : replaceRequested
                ? "No stale conflicting resident host generation exists. Explicit stale replacement did not run."
                : "No healthy resident host is connected. Use explicit stale replacement only when you want to replace the current host generation.";
        var nextAction = conflictPresent
            ? HostReconcileStaleCommand
            : HostEnsureFirstCommand;
        var nextActionKind = conflictPresent
            ? LocalHostRecommendedActions.ReconcileStaleHost
            : LocalHostRecommendedActions.EnsureHost;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness(
            conflictPresent ? "host_session_conflict" : "not_running",
            ready: false,
            nextActionKind,
            nextAction);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-reconcile.v1",
                RepoRoot = repoRoot,
                HostRunning = false,
                Replaced = false,
                ConflictPresent = conflictPresent,
                Message = message,
                ConflictingProcessId = discovery.Descriptor?.ProcessId,
                ConflictingBaseUrl = discovery.Descriptor?.BaseUrl ?? string.Empty,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES host reconcile",
            $"Repo root: {repoRoot}",
            "Replaced: False",
            $"Conflict present: {conflictPresent}",
            message,
            $"Next action: {nextAction}",
            $"Next action kind: {nextActionKind}",
            $"Lifecycle state: {lifecycle.State}",
            $"Lifecycle reason: {lifecycle.Reason}",
        };
        return new OperatorCommandResult(1, lines);
    }

    private static OperatorCommandResult RenderHostReconcileFailure(
        string repoRoot,
        bool wantsJson,
        string message,
        bool conflictPresent,
        int? conflictingProcessId,
        string? conflictingBaseUrl,
        string nextAction,
        string nextActionKind)
    {
        var lifecycle = LocalHostLifecycleProjection.FromReadiness(
            nextActionKind == LocalHostRecommendedActions.InspectHostStatus ? "host_status_unknown" : "host_session_conflict",
            ready: false,
            nextActionKind,
            nextAction);
        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-reconcile.v1",
                RepoRoot = repoRoot,
                HostRunning = false,
                Replaced = false,
                ConflictPresent = conflictPresent,
                Message = message,
                ConflictingProcessId = conflictingProcessId,
                ConflictingBaseUrl = conflictingBaseUrl ?? string.Empty,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES host reconcile",
            $"Repo root: {repoRoot}",
            "Replaced: False",
            $"Conflict present: {conflictPresent}",
            $"Message: {message}",
            $"Conflicting process id: {conflictingProcessId}",
            $"Conflicting base URL: {conflictingBaseUrl}",
            $"Next action: {nextAction}",
            $"Next action kind: {nextActionKind}",
            $"Lifecycle state: {lifecycle.State}",
            $"Lifecycle reason: {lifecycle.Reason}",
        };
        return new OperatorCommandResult(1, lines);
    }

    private static OperatorCommandResult RenderHostReconcile(
        string repoRoot,
        HostDiscoveryResult discovery,
        bool replaced,
        bool wantsJson,
        string message)
    {
        var lifecycle = LocalHostLifecycleProjection.FromReadiness("connected", ready: true, LocalHostRecommendedActions.None, "host ready");
        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-host-reconcile.v1",
                RepoRoot = repoRoot,
                HostRunning = discovery.HostRunning,
                Replaced = replaced,
                Message = message,
                BaseUrl = discovery.Summary?.BaseUrl ?? string.Empty,
                DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
                ProcessId = discovery.Descriptor?.ProcessId,
                NextActionKind = LocalHostRecommendedActions.None,
                NextAction = "host ready",
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(0, [json]);
        }

        var lines = new List<string>
        {
            "CARVES host reconcile",
            $"Repo root: {repoRoot}",
            $"Replaced: {replaced}",
            $"Message: {message}",
        };
        if (discovery.Summary is not null)
        {
            lines.Add($"Base URL: {discovery.Summary.BaseUrl}");
            lines.Add($"Dashboard: {discovery.Summary.DashboardUrl}");
        }

        lines.Add("Next action: host ready");
        lines.Add($"Next action kind: {LocalHostRecommendedActions.None}");
        lines.Add($"Lifecycle state: {lifecycle.State}");
        lines.Add($"Lifecycle reason: {lifecycle.Reason}");
        return new OperatorCommandResult(0, lines);
    }

    private static (int ProcessId, HostDiscoveryResult Discovery) StartFreshHostGeneration(string repoRoot, IReadOnlyList<string> arguments)
    {
        var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", 500, allowZero: true);
        var port = ResolveOptionalPositiveInt(arguments, "--port", FindFreePort());
        var launcher = new LocalHostProcessLauncher();
        var process = launcher.Start(repoRoot, port, intervalMilliseconds);
        var discoveryService = new LocalHostDiscoveryService();
        var discovery = BlockingHostWait.Poll(
            HostStartReadinessTimeout,
            TimeSpan.FromMilliseconds(100),
            () => discoveryService.Discover(repoRoot),
            static current => current.HostRunning);
        return (process.Id, discovery);
    }

    private static OperatorCommandResult RestartHost(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var force = commandLine.Arguments.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));
        var reason = ResolveHostCommandReason(commandLine.Arguments, "Gateway restart requested by operator.");
        var client = new LocalHostClient(commandLine.RepoRoot);
        var discovery = client.Discover();
        var previousProcessId = discovery.Descriptor?.ProcessId ?? discovery.Snapshot?.ProcessId;
        var previousBaseUrl = discovery.Summary?.BaseUrl ?? discovery.Descriptor?.BaseUrl ?? discovery.Snapshot?.BaseUrl ?? string.Empty;

        if (HasConflictingHostSession(discovery))
        {
            return RenderGatewayRestartBlocked(commandLine.RepoRoot, discovery, wantsJson);
        }

        var stoppedPrevious = false;
        if (discovery.HostRunning && discovery.Summary is not null)
        {
            var stop = client.Stop(reason, force);
            if (stop.ExitCode != 0)
            {
                return RenderGatewayRestartFailure(
                    commandLine.RepoRoot,
                    wantsJson,
                    "Gateway restart failed while stopping the current resident host generation.",
                    previousProcessId,
                    previousBaseUrl,
                    stop.Lines);
            }

            stoppedPrevious = true;
        }

        using var startupLock = LocalHostStartupLock.TryAcquire(commandLine.RepoRoot);
        if (startupLock is null)
        {
            return RenderHostStartupInProgress(commandLine.RepoRoot, wantsJson);
        }

        var (processId, restartDiscovery) = StartFreshHostGeneration(commandLine.RepoRoot, commandLine.Arguments);
        if (!restartDiscovery.HostRunning || restartDiscovery.Summary is null)
        {
            return RenderGatewayRestartFailure(
                commandLine.RepoRoot,
                wantsJson,
                "Gateway restart stopped the previous generation but the replacement did not become healthy.",
                previousProcessId,
                previousBaseUrl,
                [$"Replacement process id: {processId}", restartDiscovery.Message]);
        }

        return RenderGatewayRestartSuccess(
            commandLine.RepoRoot,
            restartDiscovery,
            wantsJson,
            stoppedPrevious,
            previousProcessId,
            previousBaseUrl,
            processId);
    }

    private static OperatorCommandResult RenderGatewayRestartSuccess(
        string repoRoot,
        HostDiscoveryResult discovery,
        bool wantsJson,
        bool stoppedPrevious,
        int? previousProcessId,
        string previousBaseUrl,
        int processId)
    {
        var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        var gatewayReady = discovery.HostRunning && discovery.Summary is not null && commandSurfaceCompatibility.Compatible;
        var message = stoppedPrevious
            ? "Gateway restarted successfully."
            : "Gateway was not running; started a fresh generation.";
        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-restart.v1",
                RepoRoot = repoRoot,
                GatewayReady = gatewayReady,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                Restarted = stoppedPrevious,
                StoppedPrevious = stoppedPrevious,
                Started = discovery.HostRunning,
                PreviousProcessId = previousProcessId,
                PreviousBaseUrl = previousBaseUrl,
                ProcessId = discovery.Descriptor?.ProcessId ?? processId,
                BaseUrl = discovery.Summary?.BaseUrl ?? string.Empty,
                DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
                Message = message,
                CommandSurfaceCompatible = commandSurfaceCompatibility.Compatible,
                CommandSurfaceReadiness = commandSurfaceCompatibility.Readiness,
                CommandSurfaceReason = commandSurfaceCompatibility.Reason,
                NextAction = gatewayReady ? "gateway ready" : "carves gateway doctor --json",
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(gatewayReady ? 0 : 1, [json]);
        }

        return new OperatorCommandResult(gatewayReady ? 0 : 1, [
            "CARVES gateway restart",
            $"Repo root: {repoRoot}",
            $"Restarted previous generation: {stoppedPrevious}",
            $"Started: {discovery.HostRunning}",
            $"Gateway ready: {gatewayReady}",
            $"Previous process id: {previousProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
            $"Previous base URL: {FormatLogPath(previousBaseUrl)}",
            $"Process id: {discovery.Descriptor?.ProcessId ?? processId}",
            $"Base URL: {discovery.Summary?.BaseUrl ?? "(none)"}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Command surface: {commandSurfaceCompatibility.Readiness}",
            $"Message: {message}",
            $"Next action: {(gatewayReady ? "gateway ready" : "carves gateway doctor --json")}",
        ]);
    }

    private static OperatorCommandResult RenderGatewayRestartBlocked(string repoRoot, HostDiscoveryResult discovery, bool wantsJson)
    {
        var descriptor = discovery.Descriptor
            ?? throw new InvalidOperationException("Expected a resident host descriptor for gateway restart conflict rendering.");
        const string nextAction = "carves gateway reconcile --replace-stale --json";
        const string nextActionKind = LocalHostRecommendedActions.ReconcileStaleHost;
        var lifecycle = LocalHostLifecycleProjection.FromReadiness("host_session_conflict", ready: false, nextActionKind, nextAction);
        var message = "Gateway restart refused to replace a stale conflicting host generation automatically.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-restart.v1",
                RepoRoot = repoRoot,
                GatewayReady = false,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                Restarted = false,
                StoppedPrevious = false,
                Started = false,
                ConflictPresent = true,
                ConflictingProcessId = descriptor.ProcessId,
                ConflictingBaseUrl = descriptor.BaseUrl,
                Message = message,
                NextActionKind = nextActionKind,
                NextAction = nextAction,
                Lifecycle = lifecycle,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        return new OperatorCommandResult(1, [
            "CARVES gateway restart",
            $"Repo root: {repoRoot}",
            "Gateway ready: False",
            "Restarted previous generation: False",
            "Started: False",
            "Conflict present: True",
            $"Conflicting process id: {descriptor.ProcessId}",
            $"Conflicting base URL: {descriptor.BaseUrl}",
            message,
            $"Next action: {nextAction}",
            $"Next action kind: {nextActionKind}",
            $"Lifecycle state: {lifecycle.State}",
            $"Lifecycle reason: {lifecycle.Reason}",
        ]);
    }

    private static OperatorCommandResult RenderGatewayRestartFailure(
        string repoRoot,
        bool wantsJson,
        string message,
        int? previousProcessId,
        string previousBaseUrl,
        IReadOnlyList<string> details)
    {
        const string nextAction = "carves gateway doctor --json";
        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-restart.v1",
                RepoRoot = repoRoot,
                GatewayReady = false,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                Restarted = false,
                Started = false,
                PreviousProcessId = previousProcessId,
                PreviousBaseUrl = previousBaseUrl,
                Message = message,
                Details = details,
                NextAction = nextAction,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(1, [json]);
        }

        return new OperatorCommandResult(1, [
            "CARVES gateway restart",
            $"Repo root: {repoRoot}",
            "Gateway ready: False",
            $"Previous process id: {previousProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
            $"Previous base URL: {FormatLogPath(previousBaseUrl)}",
            $"Message: {message}",
            .. details,
            $"Next action: {nextAction}",
        ]);
    }

    private static string ResolveHostCommandReason(IReadOnlyList<string> arguments, string defaultReason)
    {
        var reasonParts = new List<string>();
        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(argument, "--port", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--interval-ms", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--require-capability", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--tail", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            reasonParts.Add(argument);
        }

        return reasonParts.Count == 0
            ? defaultReason
            : string.Join(' ', reasonParts);
    }

    private static OperatorCommandResult HostStatus(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var discoveryService = new LocalHostDiscoveryService();
        var requiredCapability = ResolveOption(commandLine.Arguments, "--require-capability");
        var allowMachineDescriptorFallbackForForeignRepo =
            string.Equals(requiredCapability, "attach-flow", StringComparison.OrdinalIgnoreCase);
        var discovery = string.IsNullOrWhiteSpace(requiredCapability)
            ? discoveryService.Discover(commandLine.RepoRoot)
            : discoveryService.EnsureCompatible(
                commandLine.RepoRoot,
                [requiredCapability],
                allowMachineDescriptorFallbackForForeignRepo);
        return wantsJson
            ? RenderHostStatus(commandLine.RepoRoot, discovery, requiredCapability)
            : FormatHostDiscovery(commandLine.RepoRoot, discovery);
    }

    private static OperatorCommandResult GatewayDoctor(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var tailLineCount = ResolveOptionalPositiveInt(commandLine.Arguments, "--tail", defaultValue: 20, allowZero: true);
        var discovery = new LocalHostDiscoveryService().Discover(commandLine.RepoRoot);
        var honesty = LocalHostSurfaceHonesty.Describe(discovery);
        var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        var gatewayReady = discovery.HostRunning && discovery.Summary is not null && commandSurfaceCompatibility.Compatible;
        var logs = BuildGatewayLogProjection(discovery, tailLineCount);
        var activity = BuildGatewayActivityProjection(commandLine.RepoRoot, discovery, logs, tailLineCount, sinceUtc: null);
        var activityNextAction = BuildGatewayActivityNextAction(logs, activity);
        var recommendedAction = gatewayReady ? "gateway ready" : ToGatewayAction(honesty.RecommendedAction);
        var summary = gatewayReady
            ? "Gateway is ready for resident connection, routing, and observability."
            : "Gateway is not ready; use the recommended action before relying on resident routing.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-doctor.v14",
                RepoRoot = commandLine.RepoRoot,
                GatewayReady = gatewayReady,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityEventCatalogVersion = GatewayActivityEventKinds.CatalogVersion,
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = activity.ActivityStoreOperationalPosture,
                ActivityStoreOperatorSummary = activity.ActivityStoreOperatorSummary,
                ActivityStoreIssueCount = activity.ActivityStoreIssueCount,
                ActivityStoreIssues = activity.ActivityStoreIssues,
                ActivityStoreRecommendedAction = activity.ActivityStoreRecommendedAction,
                ActivityStoreVerificationFreshnessMode = activity.ActivityStoreVerificationFreshnessMode,
                ActivityStoreVerificationFreshnessThresholdHours = activity.ActivityStoreVerificationFreshnessThresholdHours,
                ActivityStoreVerificationFreshnessPosture = activity.ActivityStoreVerificationFreshnessPosture,
                ActivityStoreVerificationFreshnessAgeMinutes = activity.ActivityStoreVerificationFreshnessAgeMinutes,
                ActivityStoreVerificationFreshnessRecommendedAction = activity.ActivityStoreVerificationFreshnessRecommendedAction,
                ActivityStoreVerificationCurrentProof = activity.ActivityStoreVerificationCurrentProof,
                ActivityStoreVerificationCurrentProofReason = activity.ActivityStoreVerificationCurrentProofReason,
                ActivityStoreMaintenanceFreshnessMode = activity.ActivityStoreMaintenanceFreshnessMode,
                ActivityStoreMaintenanceFreshnessThresholdDays = activity.ActivityStoreMaintenanceFreshnessThresholdDays,
                ActivityStoreMaintenanceFreshnessPosture = activity.ActivityStoreMaintenanceFreshnessPosture,
                ActivityStoreMaintenanceFreshnessAgeMinutes = activity.ActivityStoreMaintenanceFreshnessAgeMinutes,
                ActivityStoreMaintenanceFreshnessRecommendedAction = activity.ActivityStoreMaintenanceFreshnessRecommendedAction,
                ActivityStoreMaturityStage = activity.ActivityStoreMaturityStage,
                ActivityStoreMaturityReady = activity.ActivityStoreMaturityReady,
                ActivityStoreMaturityPosture = activity.ActivityStoreMaturityPosture,
                ActivityStoreMaturitySummary = activity.ActivityStoreMaturitySummary,
                ActivityStoreMaturityLimitations = activity.ActivityStoreMaturityLimitations,
                ActivityStoreRetentionMode = activity.ActivityStoreRetentionMode,
                ActivityStoreRetentionExecutionMode = activity.ActivityStoreRetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = activity.ActivityStoreDefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = activity.ActivityStoreWriterLockMode,
                ActivityStoreWriterLockPath = activity.ActivityStoreWriterLockPath,
                ActivityStoreWriterLockExists = activity.ActivityStoreWriterLockExists,
                ActivityStoreWriterLockFileExists = activity.ActivityStoreWriterLockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = activity.ActivityStoreWriterLockCurrentlyHeld,
                ActivityStoreWriterLockStatus = activity.ActivityStoreWriterLockStatus,
                ActivityStoreWriterLockLastHolderProcessId = activity.ActivityStoreWriterLockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = activity.ActivityStoreWriterLockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = activity.ActivityStoreWriterLockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = activity.ActivityStoreIntegrityMode,
                ActivityStoreDropTelemetrySchemaVersion = GatewayActivityJournal.DropTelemetrySchemaVersion,
                ActivityStoreDropTelemetryPath = activity.ActivityStoreDropTelemetryPath,
                ActivityStoreDropTelemetryExists = activity.ActivityStoreDropTelemetryExists,
                ActivityStoreDroppedActivityCount = activity.ActivityStoreDroppedActivityCount,
                ActivityStoreLastDropAtUtc = activity.ActivityStoreLastDropAtUtc,
                ActivityStoreLastDropReason = activity.ActivityStoreLastDropReason,
                ActivityStoreLastDropEvent = activity.ActivityStoreLastDropEvent,
                ActivityStoreLastDropRequestId = activity.ActivityStoreLastDropRequestId,
                ActivityStoreEnvelopeRequiredFields = GatewayActivityEventKinds.StoreEnvelopeRequiredFields,
                ActivityStoreStableQueryFields = GatewayActivityEventKinds.StoreStableQueryFields,
                ActivityStoreForbiddenFieldFamilies = GatewayActivityEventKinds.StoreForbiddenFieldFamilies,
                ActivityStoreRouteCoverageFamilies = GatewayActivityEventKinds.StoreRouteCoverageFamilies,
                KnownActivityEventKinds = GatewayActivityEventKinds.All,
                KnownActivityCategories = GatewayActivityEventKinds.AllCategories,
                HostRunning = discovery.HostRunning,
                HostReadiness = honesty.HostReadiness,
                HostOperationalState = honesty.OperationalState,
                ConflictPresent = honesty.ConflictPresent,
                SafeToStartNewHost = honesty.SafeToStartNewHost,
                PointerRepairApplied = honesty.PointerRepairApplied,
                RecommendedActionKind = honesty.RecommendedActionKind,
                RecommendedAction = recommendedAction,
                Lifecycle = honesty.Lifecycle,
                Message = honesty.SummaryMessage,
                Summary = summary,
                BaseUrl = discovery.Summary?.BaseUrl ?? honesty.BaseUrl ?? string.Empty,
                DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
                ProcessId = honesty.ProcessId,
                CommandSurfaceCompatible = commandSurfaceCompatibility.Compatible,
                CommandSurfaceReadiness = commandSurfaceCompatibility.Readiness,
                CommandSurfaceReason = commandSurfaceCompatibility.Reason,
                LogsAvailable = logs.LogsAvailable,
                ActivityJournalSchemaVersion = GatewayActivityJournal.SchemaVersion,
                ActivitySource = activity.ActivitySource,
                ActivityJournalPath = activity.ActivityJournalPath,
                ActivityJournalStorageMode = activity.ActivityJournalStorageMode,
                ActivityJournalStoreDirectory = activity.ActivityJournalStoreDirectory,
                ActivityJournalSegmentDirectory = activity.ActivityJournalSegmentDirectory,
                ActivityJournalActiveSegmentPath = activity.ActivityJournalActiveSegmentPath,
                ActivityJournalSegmentCount = activity.ActivityJournalSegmentCount,
                ActivityJournalManifestPath = activity.ActivityJournalManifestPath,
                ActivityJournalManifestExists = activity.ActivityJournalManifestExists,
                ActivityJournalManifestSchemaVersion = activity.ActivityJournalManifestSchemaVersion,
                ActivityJournalManifestGeneratedAtUtc = activity.ActivityJournalManifestGeneratedAtUtc,
                ActivityJournalManifestRecordCount = activity.ActivityJournalManifestRecordCount,
                ActivityJournalManifestByteCount = activity.ActivityJournalManifestByteCount,
                ActivityJournalManifestFirstTimestampUtc = activity.ActivityJournalManifestFirstTimestampUtc,
                ActivityJournalManifestLastTimestampUtc = activity.ActivityJournalManifestLastTimestampUtc,
                ActivityJournalSegments = activity.ActivityJournalSegments,
                ActivityJournalLegacyPath = activity.ActivityJournalLegacyPath,
                ActivityJournalLegacyExists = activity.ActivityJournalLegacyExists,
                ActivityJournalLegacyFallbackUsed = activity.ActivityJournalLegacyFallbackUsed,
                ActivityJournalExists = activity.ActivityJournalExists,
                ActivityJournalByteCount = activity.ActivityJournalByteCount,
                ActivityJournalLineCount = activity.ActivityJournalLineCount,
                DeploymentDirectory = logs.DeploymentDirectory,
                StandardOutputLogPath = logs.StandardOutputLogPath,
                StandardErrorLogPath = logs.StandardErrorLogPath,
                StandardOutputLogExists = logs.StandardOutputLogExists,
                StandardErrorLogExists = logs.StandardErrorLogExists,
                TailLineCount = logs.TailLineCount,
                StandardOutputTail = logs.StandardOutputTail,
                StandardErrorTail = logs.StandardErrorTail,
                ActivityAvailable = activity.ActivityAvailable,
                ActivityEntryCount = activity.EntryCount,
                ActivityRequestEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRequest),
                ActivityFeedbackEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryFeedback),
                ActivityCliInvokeEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryCliInvoke),
                ActivityRestRequestEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRestRequest),
                ActivitySessionMessageEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategorySessionMessage),
                ActivityOperationFeedbackEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryOperationFeedback),
                ActivityRouteNotFoundEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRouteNotFound),
                ActivityGatewayRequestEntryCount = CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryGatewayRequest),
                LastActivityAt = FormatActivityTimestamp(activity.LastEntry),
                LastActivityEvent = activity.LastEntry?.Event ?? string.Empty,
                LastActivityCommand = ResolveActivityField(activity.LastEntry, "command"),
                LastActivityArguments = ResolveActivityField(activity.LastEntry, "arguments"),
                LastActivityExitCode = ResolveActivityField(activity.LastEntry, "exit_code"),
                LastActivityOperationId = ResolveActivityField(activity.LastEntry, "operation_id"),
                LastRequestAt = FormatActivityTimestamp(activity.LastRequest),
                LastRequestCommand = ResolveActivityField(activity.LastRequest, "command"),
                LastRequestArguments = ResolveActivityField(activity.LastRequest, "arguments"),
                LastFeedbackAt = FormatActivityTimestamp(activity.LastFeedback),
                LastFeedbackEvent = activity.LastFeedback?.Event ?? string.Empty,
                LastFeedbackCommand = ResolveActivityField(activity.LastFeedback, "command"),
                LastFeedbackExitCode = ResolveActivityField(activity.LastFeedback, "exit_code"),
                LastFeedbackOperationId = ResolveActivityField(activity.LastFeedback, "operation_id"),
                LastCliInvokeAt = FormatActivityTimestamp(activity.LastCliInvoke),
                LastCliInvokeEvent = activity.LastCliInvoke?.Event ?? string.Empty,
                LastCliInvokeCommand = ResolveActivityField(activity.LastCliInvoke, "command"),
                LastCliInvokeArguments = ResolveActivityField(activity.LastCliInvoke, "arguments"),
                LastCliInvokeExitCode = ResolveActivityField(activity.LastCliInvoke, "exit_code"),
                LastCliInvokeOperationId = ResolveActivityField(activity.LastCliInvoke, "operation_id"),
                LastRestRequestAt = FormatActivityTimestamp(activity.LastRestRequest),
                LastRestRequestEvent = activity.LastRestRequest?.Event ?? string.Empty,
                LastRestRequestSessionId = ResolveActivityField(activity.LastRestRequest, "session_id"),
                LastRestRequestMessageId = ResolveActivityField(activity.LastRestRequest, "message_id"),
                LastRestRequestOperationId = ResolveActivityField(activity.LastRestRequest, "operation_id"),
                LastSessionMessageAt = FormatActivityTimestamp(activity.LastSessionMessage),
                LastSessionMessageEvent = activity.LastSessionMessage?.Event ?? string.Empty,
                LastSessionMessageSessionId = ResolveActivityField(activity.LastSessionMessage, "session_id"),
                LastSessionMessageMessageId = ResolveActivityField(activity.LastSessionMessage, "message_id"),
                LastSessionMessageClassifiedIntent = ResolveActivityField(activity.LastSessionMessage, "classified_intent"),
                LastSessionMessageOperationId = ResolveActivityField(activity.LastSessionMessage, "operation_id"),
                LastOperationFeedbackAt = FormatActivityTimestamp(activity.LastOperationFeedback),
                LastOperationFeedbackEvent = activity.LastOperationFeedback?.Event ?? string.Empty,
                LastOperationFeedbackOperationId = ResolveActivityField(activity.LastOperationFeedback, "operation_id"),
                LastOperationFeedbackRequestedAction = ResolveActivityField(activity.LastOperationFeedback, "requested_action"),
                LastOperationFeedbackState = ResolveActivityField(activity.LastOperationFeedback, "operation_state"),
                LastOperationFeedbackCompleted = ResolveActivityField(activity.LastOperationFeedback, "completed"),
                LastOperationFeedbackExitCode = ResolveActivityField(activity.LastOperationFeedback, "exit_code"),
                LastRouteNotFoundAt = FormatActivityTimestamp(activity.LastRouteNotFound),
                LastRouteNotFoundEvent = activity.LastRouteNotFound?.Event ?? string.Empty,
                LastRouteNotFoundMethod = ResolveActivityField(activity.LastRouteNotFound, "method"),
                LastRouteNotFoundPath = ResolveActivityField(activity.LastRouteNotFound, "path"),
                LastRouteNotFoundStatus = ResolveActivityField(activity.LastRouteNotFound, "status"),
                LastGatewayRequestAt = FormatActivityTimestamp(activity.LastGatewayRequest),
                LastGatewayRequestEvent = activity.LastGatewayRequest?.Event ?? string.Empty,
                LastGatewayRequestMethod = ResolveActivityField(activity.LastGatewayRequest, "method"),
                LastGatewayRequestPath = ResolveActivityField(activity.LastGatewayRequest, "path"),
                LastGatewayRequestRemote = ResolveActivityField(activity.LastGatewayRequest, "remote"),
                ActivityNextAction = activityNextAction.Action,
                ActivityNextActionSource = activityNextAction.Source,
                ActivityNextActionPriority = activityNextAction.Priority,
                ActivityNextActionReason = activityNextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(gatewayReady ? 0 : 1, [json]);
        }

        return new OperatorCommandResult(gatewayReady ? 0 : 1, [
            "CARVES gateway doctor",
            $"Repo root: {commandLine.RepoRoot}",
            $"Gateway ready: {gatewayReady}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Host running: {discovery.HostRunning}",
            $"Host readiness: {honesty.HostReadiness}",
            $"Host operational state: {honesty.OperationalState}",
            $"Conflict present: {honesty.ConflictPresent}",
            $"Safe to start new host: {honesty.SafeToStartNewHost}",
            $"Command surface: {commandSurfaceCompatibility.Readiness}",
            $"Command surface reason: {commandSurfaceCompatibility.Reason}",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store not for: {string.Join(", ", GatewayActivityEventKinds.StoreNotFor)}",
            $"Activity store operational posture: {activity.ActivityStoreOperationalPosture}",
            $"Activity store operator summary: {activity.ActivityStoreOperatorSummary}",
            $"Activity store issue count: {activity.ActivityStoreIssueCount}",
            $"Activity store issues: {FormatActivityIssues(activity.ActivityStoreIssues)}",
            $"Activity store recommended action: {activity.ActivityStoreRecommendedAction}",
            $"Activity store verification freshness: {activity.ActivityStoreVerificationFreshnessPosture}",
            $"Activity store verification current proof: {activity.ActivityStoreVerificationCurrentProof}",
            $"Activity store verification current proof reason: {activity.ActivityStoreVerificationCurrentProofReason}",
            $"Activity store maintenance freshness: {activity.ActivityStoreMaintenanceFreshnessPosture}",
            $"Activity store maturity stage: {activity.ActivityStoreMaturityStage}",
            $"Activity store maturity ready: {activity.ActivityStoreMaturityReady}",
            $"Activity store maturity posture: {activity.ActivityStoreMaturityPosture}",
            $"Activity store maturity summary: {activity.ActivityStoreMaturitySummary}",
            $"Activity store schema: {GatewayActivityEventKinds.StoreSchemaPath}",
            $"Activity store retention mode: {activity.ActivityStoreRetentionMode}",
            $"Activity store retention execution mode: {activity.ActivityStoreRetentionExecutionMode}",
            $"Activity store default archive before days: {activity.ActivityStoreDefaultArchiveBeforeDays}",
            $"Activity store writer lock mode: {activity.ActivityStoreWriterLockMode}",
            $"Activity store writer lock: {FormatLogPath(activity.ActivityStoreWriterLockPath)}",
            $"Activity store writer lock file exists: {activity.ActivityStoreWriterLockFileExists}",
            $"Activity store writer lock currently held: {activity.ActivityStoreWriterLockCurrentlyHeld}",
            $"Activity store writer lock status: {FormatActivityValue(activity.ActivityStoreWriterLockStatus)}",
            $"Activity store writer lock last holder pid: {FormatActivityValue(activity.ActivityStoreWriterLockLastHolderProcessId)}",
            $"Activity store writer lock last acquired UTC: {FormatActivityValue(activity.ActivityStoreWriterLockLastAcquiredAtUtc)}",
            $"Activity store writer lock timeout ms: {activity.ActivityStoreWriterLockAcquireTimeoutMs}",
            $"Activity store integrity mode: {activity.ActivityStoreIntegrityMode}",
            $"Activity store drop telemetry schema: {GatewayActivityJournal.DropTelemetrySchemaVersion}",
            $"Activity store drop telemetry: {FormatLogPath(activity.ActivityStoreDropTelemetryPath)}",
            $"Activity store drop telemetry exists: {activity.ActivityStoreDropTelemetryExists}",
            $"Activity store dropped activity count: {activity.ActivityStoreDroppedActivityCount}",
            $"Activity store last drop UTC: {FormatActivityValue(activity.ActivityStoreLastDropAtUtc)}",
            $"Activity store last drop reason: {FormatActivityValue(activity.ActivityStoreLastDropReason)}",
            $"Activity store last drop event: {FormatActivityValue(activity.ActivityStoreLastDropEvent)}",
            $"Activity store last drop request id: {FormatActivityValue(activity.ActivityStoreLastDropRequestId)}",
            $"Logs available: {logs.LogsAvailable}",
            $"Activity source: {activity.ActivitySource}",
            $"Activity journal: {FormatLogPath(activity.ActivityJournalPath)}",
            $"Activity journal storage mode: {activity.ActivityJournalStorageMode}",
            $"Activity journal segment directory: {FormatLogPath(activity.ActivityJournalSegmentDirectory)}",
            $"Activity journal active segment: {FormatLogPath(activity.ActivityJournalActiveSegmentPath)}",
            $"Activity journal segment count: {activity.ActivityJournalSegmentCount}",
            $"Activity journal manifest: {FormatLogPath(activity.ActivityJournalManifestPath)}",
            $"Activity journal manifest exists: {activity.ActivityJournalManifestExists}",
            $"Activity journal manifest schema: {FormatActivityValue(activity.ActivityJournalManifestSchemaVersion)}",
            $"Activity journal manifest records: {activity.ActivityJournalManifestRecordCount}",
            $"Activity journal manifest bytes: {activity.ActivityJournalManifestByteCount}",
            $"Activity journal manifest first UTC: {FormatActivityValue(activity.ActivityJournalManifestFirstTimestampUtc)}",
            $"Activity journal manifest last UTC: {FormatActivityValue(activity.ActivityJournalManifestLastTimestampUtc)}",
            $"Activity journal legacy fallback used: {activity.ActivityJournalLegacyFallbackUsed}",
            $"Activity journal bytes: {activity.ActivityJournalByteCount}",
            $"Activity journal lines: {activity.ActivityJournalLineCount}",
            $"Host stdout log: {FormatLogPath(logs.StandardOutputLogPath)}",
            $"Host stderr log: {FormatLogPath(logs.StandardErrorLogPath)}",
            $"Activity available: {activity.ActivityAvailable}",
            $"Activity entries: {activity.EntryCount}",
            $"Activity request entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRequest)}",
            $"Activity feedback entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryFeedback)}",
            $"Activity CLI invoke entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryCliInvoke)}",
            $"Activity REST request entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRestRequest)}",
            $"Activity session message entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategorySessionMessage)}",
            $"Activity operation feedback entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryOperationFeedback)}",
            $"Activity route not found entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryRouteNotFound)}",
            $"Activity gateway request entries: {CountGatewayActivityEntries(activity.Entries, GatewayActivityEventKinds.CategoryGatewayRequest)}",
            $"Last activity: {FormatActivitySummary(activity.LastEntry)}",
            $"Last request: {FormatActivitySummary(activity.LastRequest)}",
            $"Last feedback: {FormatActivitySummary(activity.LastFeedback)}",
            $"Last CLI invoke: {FormatActivitySummary(activity.LastCliInvoke)}",
            $"Last REST request: {FormatActivitySummary(activity.LastRestRequest)}",
            $"Last session message: {FormatActivitySummary(activity.LastSessionMessage)}",
            $"Last operation feedback: {FormatActivitySummary(activity.LastOperationFeedback)}",
            $"Last route not found: {FormatActivitySummary(activity.LastRouteNotFound)}",
            $"Last gateway request: {FormatActivitySummary(activity.LastGatewayRequest)}",
            $"Summary: {summary}",
            $"Activity next action: {activityNextAction.Action}",
            $"Activity next action source: {activityNextAction.Source}",
            $"Activity next action priority: {activityNextAction.Priority}",
            $"Activity next action reason: {activityNextAction.Reason}",
            $"Recommended action: {recommendedAction}",
        ]);
    }

    private static OperatorCommandResult GatewayLogs(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var tailLineCount = ResolveOptionalPositiveInt(commandLine.Arguments, "--tail", defaultValue: 80, allowZero: true);
        var discovery = new LocalHostDiscoveryService().Discover(commandLine.RepoRoot);
        var logs = BuildGatewayLogProjection(discovery, tailLineCount);
        var message = logs.LogsAvailable
            ? "Gateway log files are available."
            : "Gateway log files are not available yet; start the gateway before expecting resident process logs.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-logs.v1",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                LogsAvailable = logs.LogsAvailable,
                Message = message,
                DeploymentDirectory = logs.DeploymentDirectory,
                StandardOutputLogPath = logs.StandardOutputLogPath,
                StandardErrorLogPath = logs.StandardErrorLogPath,
                StandardOutputLogExists = logs.StandardOutputLogExists,
                StandardErrorLogExists = logs.StandardErrorLogExists,
                TailLineCount = logs.TailLineCount,
                StandardOutputTail = logs.StandardOutputTail,
                StandardErrorTail = logs.StandardErrorTail,
                NextAction = logs.LogsAvailable ? "gateway logs available" : "carves gateway start",
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(logs.LogsAvailable ? 0 : 1, [json]);
        }

        return new OperatorCommandResult(logs.LogsAvailable ? 0 : 1, [
            "CARVES gateway logs",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Logs available: {logs.LogsAvailable}",
            $"Message: {message}",
            $"Deployment directory: {FormatLogPath(logs.DeploymentDirectory)}",
            $"Host stdout log: {FormatLogPath(logs.StandardOutputLogPath)}",
            $"Host stderr log: {FormatLogPath(logs.StandardErrorLogPath)}",
            $"Tail lines: {logs.TailLineCount}",
            "Stdout tail:",
            .. PrefixTail(logs.StandardOutputTail),
            "Stderr tail:",
            .. PrefixTail(logs.StandardErrorTail),
            $"Next action: {(logs.LogsAvailable ? "gateway logs available" : "carves gateway start")}",
        ]);
    }

    private static OperatorCommandResult GatewayActivity(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var compactRequested = commandLine.Arguments.Any(argument => string.Equals(argument, "--compact", StringComparison.OrdinalIgnoreCase));
        var retainLineCount = ResolveOptionalPositiveInt(
            commandLine.Arguments,
            "--retain",
            GatewayActivityJournal.DefaultCompactRetainLineCount);
        var tailLineCount = ResolveOptionalPositiveInt(commandLine.Arguments, "--tail", defaultValue: 200, allowZero: true);
        var filter = ResolveGatewayActivityFilter(commandLine.Arguments);
        if (!filter.Valid)
        {
            return OperatorCommandResult.Failure(filter.Error);
        }

        var discovery = new LocalHostDiscoveryService().InspectCached(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var compactResult = compactRequested
            ? GatewayActivityJournal.Compact(activityJournalPath, retainLineCount)
            : GatewayActivityJournal.NotRequested(activityJournalPath, retainLineCount);
        var logs = BuildGatewayLogProjection(discovery, tailLineCount);
        var activity = BuildGatewayActivityProjection(commandLine.RepoRoot, discovery, logs, tailLineCount, filter.SinceUtc);
        var activityNextAction = BuildGatewayActivityNextAction(logs, activity);
        var sourceEntries = activity.Entries;
        var entries = ApplyGatewayActivityFilter(sourceEntries, filter);
        var activityAvailable = entries.Count > 0;
        var projectionAvailable = logs.LogsAvailable || activity.ActivityJournalExists;
        var message = activityAvailable
            ? "Gateway activity entries are available."
            : filter.Applied && activity.ActivityAvailable
                ? "Gateway activity entries are available, but none matched the selected filter."
            : activity.ActivityJournalExists
                ? filter.SinceUtc is not null
                    ? "Gateway activity journal exists, but no known CARVES request activity was found in the store-backed since window."
                    : "Gateway activity journal exists, but no known CARVES request activity was found in the selected tail window."
                : logs.LogsAvailable
                    ? filter.SinceUtc is not null
                        ? "Gateway logs are available, but no recent CARVES request activity was found in the selected tail window fallback."
                        : "Gateway logs are available, but no recent CARVES request activity was found in the selected tail window."
                    : "Gateway activity is not available yet; start the gateway before expecting resident request activity.";

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity.v17",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                EventCatalogVersion = GatewayActivityEventKinds.CatalogVersion,
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = activity.ActivityStoreOperationalPosture,
                ActivityStoreOperatorSummary = activity.ActivityStoreOperatorSummary,
                ActivityStoreIssueCount = activity.ActivityStoreIssueCount,
                ActivityStoreIssues = activity.ActivityStoreIssues,
                ActivityStoreRecommendedAction = activity.ActivityStoreRecommendedAction,
                ActivityStoreVerificationFreshnessMode = activity.ActivityStoreVerificationFreshnessMode,
                ActivityStoreVerificationFreshnessThresholdHours = activity.ActivityStoreVerificationFreshnessThresholdHours,
                ActivityStoreVerificationFreshnessPosture = activity.ActivityStoreVerificationFreshnessPosture,
                ActivityStoreVerificationFreshnessAgeMinutes = activity.ActivityStoreVerificationFreshnessAgeMinutes,
                ActivityStoreVerificationFreshnessRecommendedAction = activity.ActivityStoreVerificationFreshnessRecommendedAction,
                ActivityStoreVerificationCurrentProof = activity.ActivityStoreVerificationCurrentProof,
                ActivityStoreVerificationCurrentProofReason = activity.ActivityStoreVerificationCurrentProofReason,
                ActivityStoreMaintenanceFreshnessMode = activity.ActivityStoreMaintenanceFreshnessMode,
                ActivityStoreMaintenanceFreshnessThresholdDays = activity.ActivityStoreMaintenanceFreshnessThresholdDays,
                ActivityStoreMaintenanceFreshnessPosture = activity.ActivityStoreMaintenanceFreshnessPosture,
                ActivityStoreMaintenanceFreshnessAgeMinutes = activity.ActivityStoreMaintenanceFreshnessAgeMinutes,
                ActivityStoreMaintenanceFreshnessRecommendedAction = activity.ActivityStoreMaintenanceFreshnessRecommendedAction,
                ActivityStoreMaturityStage = activity.ActivityStoreMaturityStage,
                ActivityStoreMaturityReady = activity.ActivityStoreMaturityReady,
                ActivityStoreMaturityPosture = activity.ActivityStoreMaturityPosture,
                ActivityStoreMaturitySummary = activity.ActivityStoreMaturitySummary,
                ActivityStoreMaturityLimitations = activity.ActivityStoreMaturityLimitations,
                ActivityStoreRetentionMode = activity.ActivityStoreRetentionMode,
                ActivityStoreRetentionExecutionMode = activity.ActivityStoreRetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = activity.ActivityStoreDefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = activity.ActivityStoreWriterLockMode,
                ActivityStoreWriterLockPath = activity.ActivityStoreWriterLockPath,
                ActivityStoreWriterLockExists = activity.ActivityStoreWriterLockExists,
                ActivityStoreWriterLockFileExists = activity.ActivityStoreWriterLockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = activity.ActivityStoreWriterLockCurrentlyHeld,
                ActivityStoreWriterLockStatus = activity.ActivityStoreWriterLockStatus,
                ActivityStoreWriterLockLastHolderProcessId = activity.ActivityStoreWriterLockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = activity.ActivityStoreWriterLockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = activity.ActivityStoreWriterLockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = activity.ActivityStoreIntegrityMode,
                ActivityStoreDropTelemetrySchemaVersion = GatewayActivityJournal.DropTelemetrySchemaVersion,
                ActivityStoreDropTelemetryPath = activity.ActivityStoreDropTelemetryPath,
                ActivityStoreDropTelemetryExists = activity.ActivityStoreDropTelemetryExists,
                ActivityStoreDroppedActivityCount = activity.ActivityStoreDroppedActivityCount,
                ActivityStoreLastDropAtUtc = activity.ActivityStoreLastDropAtUtc,
                ActivityStoreLastDropReason = activity.ActivityStoreLastDropReason,
                ActivityStoreLastDropEvent = activity.ActivityStoreLastDropEvent,
                ActivityStoreLastDropRequestId = activity.ActivityStoreLastDropRequestId,
                ActivityStoreEnvelopeRequiredFields = GatewayActivityEventKinds.StoreEnvelopeRequiredFields,
                ActivityStoreStableQueryFields = GatewayActivityEventKinds.StoreStableQueryFields,
                ActivityStoreForbiddenFieldFamilies = GatewayActivityEventKinds.StoreForbiddenFieldFamilies,
                ActivityStoreRouteCoverageFamilies = GatewayActivityEventKinds.StoreRouteCoverageFamilies,
                KnownEventKinds = GatewayActivityEventKinds.All,
                KnownCategories = GatewayActivityEventKinds.AllCategories,
                ActivityJournalSchemaVersion = GatewayActivityJournal.SchemaVersion,
                ActivitySource = activity.ActivitySource,
                ActivityQueryMode = activity.ActivityQueryMode,
                ActivityQueryStoreBackedSince = activity.ActivityQueryStoreBackedSince,
                ActivityQueryTailBounded = activity.ActivityQueryTailBounded,
                ActivityJournalPath = activity.ActivityJournalPath,
                ActivityJournalStorageMode = activity.ActivityJournalStorageMode,
                ActivityJournalStoreDirectory = activity.ActivityJournalStoreDirectory,
                ActivityJournalSegmentDirectory = activity.ActivityJournalSegmentDirectory,
                ActivityJournalArchiveDirectory = activity.ActivityJournalArchiveDirectory,
                ActivityJournalArchiveSegmentCount = activity.ActivityJournalArchiveSegmentCount,
                ActivityJournalArchiveByteCount = activity.ActivityJournalArchiveByteCount,
                ActivityJournalActiveSegmentPath = activity.ActivityJournalActiveSegmentPath,
                ActivityJournalSegmentCount = activity.ActivityJournalSegmentCount,
                ActivityJournalManifestPath = activity.ActivityJournalManifestPath,
                ActivityJournalManifestExists = activity.ActivityJournalManifestExists,
                ActivityJournalManifestSchemaVersion = activity.ActivityJournalManifestSchemaVersion,
                ActivityJournalCheckpointChainPath = activity.ActivityJournalCheckpointChainPath,
                ActivityJournalCheckpointChainExists = activity.ActivityJournalCheckpointChainExists,
                ActivityJournalCheckpointChainCount = activity.ActivityJournalCheckpointChainCount,
                ActivityJournalCheckpointChainLatestSequence = activity.ActivityJournalCheckpointChainLatestSequence,
                ActivityJournalCheckpointChainLatestCheckpointSha256 = activity.ActivityJournalCheckpointChainLatestCheckpointSha256,
                ActivityJournalCheckpointChainLatestManifestSha256 = activity.ActivityJournalCheckpointChainLatestManifestSha256,
                ActivityJournalManifestGeneratedAtUtc = activity.ActivityJournalManifestGeneratedAtUtc,
                ActivityJournalManifestRecordCount = activity.ActivityJournalManifestRecordCount,
                ActivityJournalManifestByteCount = activity.ActivityJournalManifestByteCount,
                ActivityJournalManifestFirstTimestampUtc = activity.ActivityJournalManifestFirstTimestampUtc,
                ActivityJournalManifestLastTimestampUtc = activity.ActivityJournalManifestLastTimestampUtc,
                ActivityJournalSegments = activity.ActivityJournalSegments,
                ActivityJournalLegacyPath = activity.ActivityJournalLegacyPath,
                ActivityJournalLegacyExists = activity.ActivityJournalLegacyExists,
                ActivityJournalLegacyFallbackUsed = activity.ActivityJournalLegacyFallbackUsed,
                ActivityJournalExists = activity.ActivityJournalExists,
                ActivityJournalByteCount = activity.ActivityJournalByteCount,
                ActivityJournalLineCount = activity.ActivityJournalLineCount,
                ActivityJournalCompactRequested = compactResult.Requested,
                ActivityJournalCompactApplied = compactResult.Applied,
                ActivityJournalCompactReason = compactResult.Reason,
                ActivityJournalCompactRetainLineLimit = compactResult.RetainLineLimit,
                ActivityJournalCompactOriginalLineCount = compactResult.OriginalLineCount,
                ActivityJournalCompactRetainedLineCount = compactResult.RetainedLineCount,
                LogsAvailable = logs.LogsAvailable,
                ActivityAvailable = activityAvailable,
                Message = message,
                StandardOutputLogPath = logs.StandardOutputLogPath,
                TailLineCount = logs.TailLineCount,
                ActivityFilterApplied = filter.Applied,
                ActivityFilterCategory = filter.Category,
                ActivityFilterEvent = filter.EventKind,
                ActivityFilterCommand = filter.Command,
                ActivityFilterPath = filter.Path,
                ActivityFilterRequestId = filter.RequestId,
                ActivityFilterOperationId = filter.OperationId,
                ActivityFilterSessionId = filter.SessionId,
                ActivityFilterMessageId = filter.MessageId,
                ActivityFilterSinceMinutes = filter.SinceMinutes,
                ActivityFilterSinceUtc = filter.SinceUtc?.ToString("O") ?? string.Empty,
                SourceEntryCount = sourceEntries.Count,
                EntryCount = entries.Count,
                RequestEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRequest),
                FeedbackEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryFeedback),
                CliInvokeEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryCliInvoke),
                RestRequestEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRestRequest),
                SessionMessageEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategorySessionMessage),
                OperationFeedbackEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryOperationFeedback),
                RouteNotFoundEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRouteNotFound),
                GatewayRequestEntryCount = CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryGatewayRequest),
                Entries = entries,
                NextAction = activityNextAction.Action,
                NextActionSource = activityNextAction.Source,
                NextActionPriority = activityNextAction.Priority,
                NextActionReason = activityNextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(projectionAvailable ? 0 : 1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES gateway activity",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity source: {activity.ActivitySource}",
            $"Activity query mode: {activity.ActivityQueryMode}",
            $"Activity query store-backed since: {activity.ActivityQueryStoreBackedSince}",
            $"Activity query tail bounded: {activity.ActivityQueryTailBounded}",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store not for: {string.Join(", ", GatewayActivityEventKinds.StoreNotFor)}",
            $"Activity store operational posture: {activity.ActivityStoreOperationalPosture}",
            $"Activity store operator summary: {activity.ActivityStoreOperatorSummary}",
            $"Activity store issue count: {activity.ActivityStoreIssueCount}",
            $"Activity store issues: {FormatActivityIssues(activity.ActivityStoreIssues)}",
            $"Activity store recommended action: {activity.ActivityStoreRecommendedAction}",
            $"Activity store verification freshness: {activity.ActivityStoreVerificationFreshnessPosture}",
            $"Activity store verification current proof: {activity.ActivityStoreVerificationCurrentProof}",
            $"Activity store verification current proof reason: {activity.ActivityStoreVerificationCurrentProofReason}",
            $"Activity store maintenance freshness: {activity.ActivityStoreMaintenanceFreshnessPosture}",
            $"Activity store maturity stage: {activity.ActivityStoreMaturityStage}",
            $"Activity store maturity ready: {activity.ActivityStoreMaturityReady}",
            $"Activity store maturity posture: {activity.ActivityStoreMaturityPosture}",
            $"Activity store maturity summary: {activity.ActivityStoreMaturitySummary}",
            $"Activity store schema: {GatewayActivityEventKinds.StoreSchemaPath}",
            $"Activity store manifest schema: {GatewayActivityEventKinds.StoreManifestSchemaPath}",
            $"Activity store checkpoint schema: {GatewayActivityEventKinds.StoreCheckpointSchemaPath}",
            $"Activity store retention mode: {activity.ActivityStoreRetentionMode}",
            $"Activity store retention execution mode: {activity.ActivityStoreRetentionExecutionMode}",
            $"Activity store default archive before days: {activity.ActivityStoreDefaultArchiveBeforeDays}",
            $"Activity store writer lock mode: {activity.ActivityStoreWriterLockMode}",
            $"Activity store writer lock: {FormatLogPath(activity.ActivityStoreWriterLockPath)}",
            $"Activity store writer lock file exists: {activity.ActivityStoreWriterLockFileExists}",
            $"Activity store writer lock currently held: {activity.ActivityStoreWriterLockCurrentlyHeld}",
            $"Activity store writer lock status: {FormatActivityValue(activity.ActivityStoreWriterLockStatus)}",
            $"Activity store writer lock last holder pid: {FormatActivityValue(activity.ActivityStoreWriterLockLastHolderProcessId)}",
            $"Activity store writer lock last acquired UTC: {FormatActivityValue(activity.ActivityStoreWriterLockLastAcquiredAtUtc)}",
            $"Activity store writer lock timeout ms: {activity.ActivityStoreWriterLockAcquireTimeoutMs}",
            $"Activity store integrity mode: {activity.ActivityStoreIntegrityMode}",
            $"Activity store drop telemetry schema: {GatewayActivityJournal.DropTelemetrySchemaVersion}",
            $"Activity store drop telemetry: {FormatLogPath(activity.ActivityStoreDropTelemetryPath)}",
            $"Activity store drop telemetry exists: {activity.ActivityStoreDropTelemetryExists}",
            $"Activity store dropped activity count: {activity.ActivityStoreDroppedActivityCount}",
            $"Activity store last drop UTC: {FormatActivityValue(activity.ActivityStoreLastDropAtUtc)}",
            $"Activity store last drop reason: {FormatActivityValue(activity.ActivityStoreLastDropReason)}",
            $"Activity store last drop event: {FormatActivityValue(activity.ActivityStoreLastDropEvent)}",
            $"Activity store last drop request id: {FormatActivityValue(activity.ActivityStoreLastDropRequestId)}",
            $"Activity journal: {FormatLogPath(activity.ActivityJournalPath)}",
            $"Activity journal storage mode: {activity.ActivityJournalStorageMode}",
            $"Activity journal segment directory: {FormatLogPath(activity.ActivityJournalSegmentDirectory)}",
            $"Activity journal archive directory: {FormatLogPath(activity.ActivityJournalArchiveDirectory)}",
            $"Activity journal archive segment count: {activity.ActivityJournalArchiveSegmentCount}",
            $"Activity journal archive bytes: {activity.ActivityJournalArchiveByteCount}",
            $"Activity journal active segment: {FormatLogPath(activity.ActivityJournalActiveSegmentPath)}",
            $"Activity journal segment count: {activity.ActivityJournalSegmentCount}",
            $"Activity journal manifest: {FormatLogPath(activity.ActivityJournalManifestPath)}",
            $"Activity journal manifest exists: {activity.ActivityJournalManifestExists}",
            $"Activity journal manifest schema: {FormatActivityValue(activity.ActivityJournalManifestSchemaVersion)}",
            $"Activity journal checkpoint chain: {FormatLogPath(activity.ActivityJournalCheckpointChainPath)}",
            $"Activity journal checkpoint chain exists: {activity.ActivityJournalCheckpointChainExists}",
            $"Activity journal checkpoint count: {activity.ActivityJournalCheckpointChainCount}",
            $"Activity journal checkpoint latest sequence: {activity.ActivityJournalCheckpointChainLatestSequence}",
            $"Activity journal checkpoint latest hash: {FormatActivityValue(activity.ActivityJournalCheckpointChainLatestCheckpointSha256)}",
            $"Activity journal checkpoint latest manifest hash: {FormatActivityValue(activity.ActivityJournalCheckpointChainLatestManifestSha256)}",
            $"Activity journal manifest records: {activity.ActivityJournalManifestRecordCount}",
            $"Activity journal manifest bytes: {activity.ActivityJournalManifestByteCount}",
            $"Activity journal manifest first UTC: {FormatActivityValue(activity.ActivityJournalManifestFirstTimestampUtc)}",
            $"Activity journal manifest last UTC: {FormatActivityValue(activity.ActivityJournalManifestLastTimestampUtc)}",
            $"Activity journal legacy fallback used: {activity.ActivityJournalLegacyFallbackUsed}",
            $"Activity journal bytes: {activity.ActivityJournalByteCount}",
            $"Activity journal lines: {activity.ActivityJournalLineCount}",
            $"Activity journal compact requested: {compactResult.Requested}",
            $"Activity journal compact applied: {compactResult.Applied}",
            $"Activity journal compact reason: {compactResult.Reason}",
            $"Activity filter applied: {filter.Applied}",
            $"Activity filter category: {filter.Category}",
            $"Activity filter event: {FormatActivityValue(filter.EventKind)}",
            $"Activity filter command: {FormatActivityValue(filter.Command)}",
            $"Activity filter path: {FormatActivityValue(filter.Path)}",
            $"Activity filter request id: {FormatActivityValue(filter.RequestId)}",
            $"Activity filter operation id: {FormatActivityValue(filter.OperationId)}",
            $"Activity filter session id: {FormatActivityValue(filter.SessionId)}",
            $"Activity filter message id: {FormatActivityValue(filter.MessageId)}",
            $"Activity filter since minutes: {filter.SinceMinutes}",
            $"Activity filter since UTC: {FormatActivityValue(filter.SinceUtc?.ToString("O") ?? string.Empty)}",
            $"Logs available: {logs.LogsAvailable}",
            $"Activity available: {activityAvailable}",
            $"Message: {message}",
            $"Host stdout log: {FormatLogPath(logs.StandardOutputLogPath)}",
            $"Tail lines: {logs.TailLineCount}",
            $"Source entry count: {sourceEntries.Count}",
            $"Entry count: {entries.Count}",
            $"Request entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRequest)}",
            $"Feedback entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryFeedback)}",
            $"CLI invoke entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryCliInvoke)}",
            $"REST request entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRestRequest)}",
            $"Session message entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategorySessionMessage)}",
            $"Operation feedback entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryOperationFeedback)}",
            $"Route not found entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryRouteNotFound)}",
            $"Gateway request entries: {CountGatewayActivityEntries(sourceEntries, GatewayActivityEventKinds.CategoryGatewayRequest)}",
        };

        if (activity.ActivityJournalSegments.Count > 0)
        {
            lines.Add("Activity journal segments:");
            foreach (var segment in activity.ActivityJournalSegments)
            {
                lines.Add($"  {segment.FileName} records={segment.RecordCount} bytes={segment.ByteCount} first={FormatActivityValue(segment.FirstTimestampUtc)} last={FormatActivityValue(segment.LastTimestampUtc)} sha256={FormatActivityValue(segment.Sha256)}");
            }
        }

        if (entries.Count == 0)
        {
            lines.Add("  (no recent CARVES request activity)");
        }
        else
        {
            foreach (var entry in entries)
            {
                lines.Add($"  {entry.Timestamp:O} {entry.Event} {FormatGatewayActivityEntryFields(entry)}");
            }
        }

        lines.Add($"Next action: {activityNextAction.Action}");
        lines.Add($"Next action source: {activityNextAction.Source}");
        lines.Add($"Next action priority: {activityNextAction.Priority}");
        lines.Add($"Next action reason: {activityNextAction.Reason}");
        return new OperatorCommandResult(projectionAvailable ? 0 : 1, lines);
    }

    private static bool IsGatewayActivityVerify(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 1
               && string.Equals(arguments[0], "activity", StringComparison.OrdinalIgnoreCase)
               && string.Equals(arguments[1], "verify", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGatewayActivityStatus(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 1
               && string.Equals(arguments[0], "activity", StringComparison.OrdinalIgnoreCase)
               && string.Equals(arguments[1], "status", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGatewayActivityMaintenance(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 1
               && string.Equals(arguments[0], "activity", StringComparison.OrdinalIgnoreCase)
               && string.Equals(arguments[1], "maintenance", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGatewayActivityArchive(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 1
               && string.Equals(arguments[0], "activity", StringComparison.OrdinalIgnoreCase)
               && string.Equals(arguments[1], "archive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGatewayActivityExplain(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 1
               && string.Equals(arguments[0], "activity", StringComparison.OrdinalIgnoreCase)
               && string.Equals(arguments[1], "explain", StringComparison.OrdinalIgnoreCase);
    }

    private static OperatorCommandResult GatewayActivityMaintenance(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var beforeDays = ResolveOptionalPositiveInt(
            commandLine.Arguments,
            "--before-days",
            GatewayActivityJournal.DefaultArchiveBeforeDays);
        var discovery = new LocalHostDiscoveryService().InspectCached(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var maintenance = GatewayActivityJournal.PlanMaintenance(activityJournalPath, beforeDays);
        var journalStatus = GatewayActivityJournal.Inspect(activityJournalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var message = maintenance.MaintenanceNeeded
            ? "Gateway activity maintenance dry-run found archive candidates."
            : maintenance.Reason switch
            {
                "segment_store_missing" => "Gateway activity maintenance dry-run found no segment store.",
                "no_archive_candidates" => "Gateway activity maintenance dry-run found no old segment candidates.",
                _ => "Gateway activity maintenance dry-run found no action to apply.",
            };
        var nextAction = BuildGatewayActivityMaintenanceNextAction(maintenance, storePosture);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity-maintenance.v3",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreManifestSchemaVersion = GatewayActivityJournal.ManifestSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = storePosture.OperationalPosture,
                ActivityStoreOperatorSummary = storePosture.OperatorSummary,
                ActivityStoreIssueCount = storePosture.IssueCount,
                ActivityStoreIssues = storePosture.Issues,
                ActivityStoreRecommendedAction = storePosture.RecommendedAction,
                ActivityStoreRetentionMode = journalStatus.RetentionMode,
                ActivityStoreRetentionExecutionMode = journalStatus.RetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = journalStatus.DefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = journalStatus.WriterLockMode,
                ActivityStoreWriterLockPath = journalStatus.LockPath,
                ActivityStoreWriterLockFileExists = journalStatus.LockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = journalStatus.LockCurrentlyHeld,
                ActivityStoreWriterLockStatus = journalStatus.LockStatus,
                ActivityStoreIntegrityMode = journalStatus.IntegrityMode,
                ActivityJournalSchemaVersion = GatewayActivityJournal.SchemaVersion,
                ActivityJournalPath = journalStatus.Path,
                ActivityJournalStorageMode = journalStatus.StorageMode,
                ActivityJournalSegmentDirectory = journalStatus.SegmentDirectory,
                ActivityJournalArchiveDirectory = journalStatus.ArchiveDirectory,
                ActivityJournalSegmentCount = journalStatus.SegmentCount,
                ActivityJournalArchiveSegmentCount = journalStatus.ArchiveSegmentCount,
                ActivityJournalArchiveByteCount = journalStatus.ArchiveByteCount,
                ActivityJournalManifestExists = journalStatus.ManifestExists,
                ActivityJournalManifestRecordCount = journalStatus.ManifestRecordCount,
                ActivityJournalManifestByteCount = journalStatus.ManifestByteCount,
                ActivityStoreMaintenanceSummarySchemaVersion = GatewayActivityJournal.MaintenanceSummarySchemaVersion,
                ActivityStoreMaintenanceSummaryPath = journalStatus.MaintenanceSummaryPath,
                ActivityStoreMaintenanceSummaryExists = journalStatus.MaintenanceSummaryExists,
                ActivityStoreLastMaintenanceAtUtc = journalStatus.LastMaintenanceAtUtc,
                ActivityStoreLastMaintenanceOperation = journalStatus.LastMaintenanceOperation,
                ActivityStoreLastMaintenanceDryRun = journalStatus.LastMaintenanceDryRun,
                ActivityStoreLastMaintenanceApplied = journalStatus.LastMaintenanceApplied,
                ActivityStoreLastMaintenanceReason = journalStatus.LastMaintenanceReason,
                ActivityStoreLastMaintenanceBeforeDays = journalStatus.LastMaintenanceBeforeDays,
                ActivityStoreLastMaintenanceArchiveBeforeUtcDate = journalStatus.LastMaintenanceArchiveBeforeUtcDate,
                ActivityStoreLastMaintenanceCandidateSegmentCount = journalStatus.LastMaintenanceCandidateSegmentCount,
                ActivityStoreLastMaintenanceArchivedSegmentCount = journalStatus.LastMaintenanceArchivedSegmentCount,
                ActivityStoreLastMaintenanceArchivedByteCount = journalStatus.LastMaintenanceArchivedByteCount,
                ActivityStoreLastMaintenanceArchivedRecordCount = journalStatus.LastMaintenanceArchivedRecordCount,
                ActivityStoreLastMaintenanceRecommendedAction = journalStatus.LastMaintenanceRecommendedAction,
                MaintenanceMode = maintenance.MaintenanceMode,
                MaintenanceDryRun = maintenance.DryRun,
                MaintenanceWillModifyStore = maintenance.WillModifyStore,
                MaintenanceNeeded = maintenance.MaintenanceNeeded,
                MaintenanceReason = maintenance.Reason,
                MaintenanceRecommendedAction = maintenance.RecommendedAction,
                MaintenanceBeforeDays = maintenance.BeforeDays,
                MaintenanceArchiveBeforeUtcDate = maintenance.ArchiveBeforeUtcDate,
                MaintenanceArchiveDirectory = maintenance.ArchiveDirectory,
                ArchiveCandidateSegmentCount = maintenance.ArchiveCandidateSegmentCount,
                ArchiveCandidateByteCount = maintenance.ArchiveCandidateByteCount,
                ArchiveCandidateRecordCount = maintenance.ArchiveCandidateRecordCount,
                ArchiveCandidateSegments = maintenance.ArchiveCandidateSegments,
                Message = message,
                NextAction = nextAction.Action,
                NextActionSource = nextAction.Source,
                NextActionPriority = nextAction.Priority,
                NextActionReason = nextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(0, [json]);
        }

        var lines = new List<string>
        {
            "CARVES gateway activity maintenance",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store operational posture: {storePosture.OperationalPosture}",
            $"Activity store operator summary: {storePosture.OperatorSummary}",
            $"Activity store issue count: {storePosture.IssueCount}",
            $"Activity store issues: {FormatActivityIssues(storePosture.Issues)}",
            $"Activity store recommended action: {storePosture.RecommendedAction}",
            $"Activity store retention mode: {journalStatus.RetentionMode}",
            $"Activity store retention execution mode: {journalStatus.RetentionExecutionMode}",
            $"Activity store default archive before days: {journalStatus.DefaultArchiveBeforeDays}",
            $"Activity store writer lock status: {FormatActivityValue(journalStatus.LockStatus)}",
            $"Activity store writer lock currently held: {journalStatus.LockCurrentlyHeld}",
            $"Activity journal segment count: {journalStatus.SegmentCount}",
            $"Activity journal archive segment count: {journalStatus.ArchiveSegmentCount}",
            $"Activity journal manifest records: {journalStatus.ManifestRecordCount}",
            $"Activity store maintenance summary exists: {journalStatus.MaintenanceSummaryExists}",
            $"Activity store last maintenance UTC: {FormatActivityValue(journalStatus.LastMaintenanceAtUtc)}",
            $"Activity store last maintenance operation: {FormatActivityValue(journalStatus.LastMaintenanceOperation)}",
            $"Activity store last maintenance applied: {journalStatus.LastMaintenanceApplied}",
            $"Activity store last maintenance reason: {FormatActivityValue(journalStatus.LastMaintenanceReason)}",
            $"Activity store last maintenance archived segments: {journalStatus.LastMaintenanceArchivedSegmentCount}",
            $"Maintenance mode: {maintenance.MaintenanceMode}",
            $"Maintenance dry run: {maintenance.DryRun}",
            $"Maintenance will modify store: {maintenance.WillModifyStore}",
            $"Maintenance needed: {maintenance.MaintenanceNeeded}",
            $"Maintenance reason: {maintenance.Reason}",
            $"Maintenance before days: {maintenance.BeforeDays}",
            $"Maintenance archive before UTC date: {maintenance.ArchiveBeforeUtcDate}",
            $"Archive candidate segment count: {maintenance.ArchiveCandidateSegmentCount}",
            $"Archive candidate bytes: {maintenance.ArchiveCandidateByteCount}",
            $"Archive candidate records: {maintenance.ArchiveCandidateRecordCount}",
            $"Message: {message}",
        };

        if (maintenance.ArchiveCandidateSegments.Count == 0)
        {
            lines.Add("Archive candidate segments: none");
        }
        else
        {
            lines.Add("Archive candidate segments:");
            foreach (var segment in maintenance.ArchiveCandidateSegments)
            {
                lines.Add($"  {segment.FileName} date={segment.SegmentDateUtc} records={segment.RecordCount} bytes={segment.ByteCount} sha256={FormatActivityValue(segment.Sha256)} archive={FormatLogPath(segment.ArchivePath)}");
            }
        }

        lines.Add($"Next action: {nextAction.Action}");
        lines.Add($"Next action source: {nextAction.Source}");
        lines.Add($"Next action priority: {nextAction.Priority}");
        lines.Add($"Next action reason: {nextAction.Reason}");
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult GatewayActivityStatus(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var requiresMaturity = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--require-maturity", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "--strict-maturity", StringComparison.OrdinalIgnoreCase));
        var discovery = new LocalHostDiscoveryService().InspectCached(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var journalStatus = GatewayActivityJournal.Inspect(activityJournalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var maintenanceFreshness = BuildGatewayActivityMaintenanceFreshnessPosture(journalStatus);
        var verificationFreshness = BuildGatewayActivityVerificationFreshnessPosture(journalStatus);
        var nextAction = BuildGatewayActivityStatusNextAction(storePosture, verificationFreshness, maintenanceFreshness);
        var maturity = BuildGatewayActivityMaturityPosture(storePosture, verificationFreshness, maintenanceFreshness);
        var operationalOk = storePosture.IssueCount == 0;
        var exitOk = operationalOk && (!requiresMaturity || maturity.Ready);
        var exitPolicy = requiresMaturity
            ? "operational_and_maturity_ready"
            : "operational_only";
        var exitReason = ResolveGatewayActivityStatusExitReason(operationalOk, requiresMaturity, storePosture, maturity);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity-status.v9",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreManifestSchemaVersion = GatewayActivityJournal.ManifestSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = storePosture.OperationalPosture,
                ActivityStoreOperatorSummary = storePosture.OperatorSummary,
                ActivityStoreIssueCount = storePosture.IssueCount,
                ActivityStoreIssues = storePosture.Issues,
                ActivityStoreRecommendedAction = storePosture.RecommendedAction,
                ActivityStoreNextAction = nextAction.Action,
                ActivityStoreNextActionSource = nextAction.Source,
                ActivityStoreNextActionPriority = nextAction.Priority,
                ActivityStoreNextActionReason = nextAction.Reason,
                ActivityStoreMaturityStage = maturity.Stage,
                ActivityStoreMaturityReady = maturity.Ready,
                ActivityStoreMaturityPosture = maturity.Posture,
                ActivityStoreMaturitySummary = maturity.Summary,
                ActivityStoreMaturityLimitations = maturity.Limitations,
                ActivityStoreOperationalOk = operationalOk,
                ActivityStoreMaturityRequired = requiresMaturity,
                ActivityStoreExitPolicy = exitPolicy,
                ActivityStoreExitOk = exitOk,
                ActivityStoreExitReason = exitReason,
                ActivityStoreRetentionMode = journalStatus.RetentionMode,
                ActivityStoreRetentionExecutionMode = journalStatus.RetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = journalStatus.DefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = journalStatus.WriterLockMode,
                ActivityStoreWriterLockPath = journalStatus.LockPath,
                ActivityStoreWriterLockExists = journalStatus.LockExists,
                ActivityStoreWriterLockFileExists = journalStatus.LockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = journalStatus.LockCurrentlyHeld,
                ActivityStoreWriterLockStatus = journalStatus.LockStatus,
                ActivityStoreWriterLockLastHolderProcessId = journalStatus.LockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = journalStatus.LockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = journalStatus.LockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = journalStatus.IntegrityMode,
                ActivityStoreDropTelemetrySchemaVersion = GatewayActivityJournal.DropTelemetrySchemaVersion,
                ActivityStoreDropTelemetryPath = journalStatus.DropTelemetryPath,
                ActivityStoreDropTelemetryExists = journalStatus.DropTelemetryExists,
                ActivityStoreDroppedActivityCount = journalStatus.DroppedActivityCount,
                ActivityStoreLastDropAtUtc = journalStatus.LastDropAtUtc,
                ActivityStoreLastDropReason = journalStatus.LastDropReason,
                ActivityStoreLastDropEvent = journalStatus.LastDropEvent,
                ActivityStoreLastDropRequestId = journalStatus.LastDropRequestId,
                ActivityStoreMaintenanceSummarySchemaVersion = GatewayActivityJournal.MaintenanceSummarySchemaVersion,
                ActivityStoreMaintenanceSummaryPath = journalStatus.MaintenanceSummaryPath,
                ActivityStoreMaintenanceSummaryExists = journalStatus.MaintenanceSummaryExists,
                ActivityStoreLastMaintenanceAtUtc = journalStatus.LastMaintenanceAtUtc,
                ActivityStoreLastMaintenanceOperation = journalStatus.LastMaintenanceOperation,
                ActivityStoreLastMaintenanceDryRun = journalStatus.LastMaintenanceDryRun,
                ActivityStoreLastMaintenanceApplied = journalStatus.LastMaintenanceApplied,
                ActivityStoreLastMaintenanceReason = journalStatus.LastMaintenanceReason,
                ActivityStoreLastMaintenanceBeforeDays = journalStatus.LastMaintenanceBeforeDays,
                ActivityStoreLastMaintenanceArchiveBeforeUtcDate = journalStatus.LastMaintenanceArchiveBeforeUtcDate,
                ActivityStoreLastMaintenanceCandidateSegmentCount = journalStatus.LastMaintenanceCandidateSegmentCount,
                ActivityStoreLastMaintenanceArchivedSegmentCount = journalStatus.LastMaintenanceArchivedSegmentCount,
                ActivityStoreLastMaintenanceArchivedByteCount = journalStatus.LastMaintenanceArchivedByteCount,
                ActivityStoreLastMaintenanceArchivedRecordCount = journalStatus.LastMaintenanceArchivedRecordCount,
                ActivityStoreLastMaintenanceRecommendedAction = journalStatus.LastMaintenanceRecommendedAction,
                ActivityStoreMaintenanceFreshnessMode = maintenanceFreshness.Mode,
                ActivityStoreMaintenanceFreshnessThresholdDays = maintenanceFreshness.ThresholdDays,
                ActivityStoreMaintenanceFreshnessPosture = maintenanceFreshness.Posture,
                ActivityStoreMaintenanceFreshnessAgeMinutes = maintenanceFreshness.AgeMinutes,
                ActivityStoreMaintenanceFreshnessRecommendedAction = maintenanceFreshness.RecommendedAction,
                ActivityStoreVerificationSummarySchemaVersion = GatewayActivityJournal.VerificationSummarySchemaVersion,
                ActivityStoreVerificationSummaryPath = journalStatus.VerificationSummaryPath,
                ActivityStoreVerificationSummaryExists = journalStatus.VerificationSummaryExists,
                ActivityStoreLastVerificationAtUtc = journalStatus.LastVerificationAtUtc,
                ActivityStoreLastVerificationPassed = journalStatus.LastVerificationPassed,
                ActivityStoreLastVerificationPosture = journalStatus.LastVerificationPosture,
                ActivityStoreLastVerificationPossiblyTransient = journalStatus.LastVerificationPossiblyTransient,
                ActivityStoreLastVerificationConsistencyMode = journalStatus.LastVerificationConsistencyMode,
                ActivityStoreLastVerificationSnapshotLockAcquired = journalStatus.LastVerificationSnapshotLockAcquired,
                ActivityStoreLastVerificationIssueCount = journalStatus.LastVerificationIssueCount,
                ActivityStoreLastVerificationStoredManifestRecordCount = journalStatus.LastVerificationStoredManifestRecordCount,
                ActivityStoreLastVerificationActualManifestRecordCount = journalStatus.LastVerificationActualManifestRecordCount,
                ActivityStoreLastVerificationStoredManifestByteCount = journalStatus.LastVerificationStoredManifestByteCount,
                ActivityStoreLastVerificationActualManifestByteCount = journalStatus.LastVerificationActualManifestByteCount,
                ActivityStoreLastVerificationRecommendedAction = journalStatus.LastVerificationRecommendedAction,
                ActivityStoreLastVerificationManifestGeneratedAtUtc = journalStatus.LastVerificationManifestGeneratedAtUtc,
                ActivityStoreLastVerificationManifestSha256 = journalStatus.LastVerificationManifestSha256,
                ActivityStoreLastVerificationCheckpointLatestSequence = journalStatus.LastVerificationCheckpointLatestSequence,
                ActivityStoreLastVerificationCheckpointLatestCheckpointSha256 = journalStatus.LastVerificationCheckpointLatestCheckpointSha256,
                ActivityStoreLastVerificationCheckpointLatestManifestSha256 = journalStatus.LastVerificationCheckpointLatestManifestSha256,
                ActivityStoreVerificationFreshnessMode = verificationFreshness.Mode,
                ActivityStoreVerificationFreshnessThresholdHours = verificationFreshness.ThresholdHours,
                ActivityStoreVerificationFreshnessPosture = verificationFreshness.Posture,
                ActivityStoreVerificationFreshnessAgeMinutes = verificationFreshness.AgeMinutes,
                ActivityStoreVerificationFreshnessRecommendedAction = verificationFreshness.RecommendedAction,
                ActivityStoreVerificationCurrentProof = verificationFreshness.CurrentProof,
                ActivityStoreVerificationCurrentProofReason = verificationFreshness.CurrentProofReason,
                ActivityJournalSchemaVersion = GatewayActivityJournal.SchemaVersion,
                ActivityJournalPath = journalStatus.Path,
                ActivityJournalStorageMode = journalStatus.StorageMode,
                ActivityJournalStoreDirectory = journalStatus.StoreDirectory,
                ActivityJournalSegmentDirectory = journalStatus.SegmentDirectory,
                ActivityJournalArchiveDirectory = journalStatus.ArchiveDirectory,
                ActivityJournalArchiveSegmentCount = journalStatus.ArchiveSegmentCount,
                ActivityJournalArchiveByteCount = journalStatus.ArchiveByteCount,
                ActivityJournalActiveSegmentPath = journalStatus.ActiveSegmentPath,
                ActivityJournalSegmentCount = journalStatus.SegmentCount,
                ActivityJournalManifestPath = journalStatus.ManifestPath,
                ActivityJournalManifestExists = journalStatus.ManifestExists,
                ActivityJournalManifestSchemaVersion = journalStatus.ManifestSchemaVersion,
                ActivityJournalManifestGeneratedAtUtc = journalStatus.ManifestGeneratedAtUtc,
                ActivityJournalManifestRecordCount = journalStatus.ManifestRecordCount,
                ActivityJournalManifestByteCount = journalStatus.ManifestByteCount,
                ActivityJournalManifestFirstTimestampUtc = journalStatus.ManifestFirstTimestampUtc,
                ActivityJournalManifestLastTimestampUtc = journalStatus.ManifestLastTimestampUtc,
                ActivityJournalCheckpointChainPath = journalStatus.CheckpointChainPath,
                ActivityJournalCheckpointChainExists = journalStatus.CheckpointChainExists,
                ActivityJournalCheckpointChainCount = journalStatus.CheckpointChainCount,
                ActivityJournalCheckpointChainLatestSequence = journalStatus.CheckpointChainLatestSequence,
                ActivityJournalCheckpointChainLatestCheckpointSha256 = journalStatus.CheckpointChainLatestCheckpointSha256,
                ActivityJournalCheckpointChainLatestManifestSha256 = journalStatus.CheckpointChainLatestManifestSha256,
                ActivityJournalLegacyPath = journalStatus.LegacyJournalPath,
                ActivityJournalLegacyExists = journalStatus.LegacyJournalExists,
                ActivityJournalLegacyFallbackUsed = journalStatus.LegacyFallbackUsed,
                ActivityJournalExists = journalStatus.Exists,
                ActivityJournalByteCount = journalStatus.ByteCount,
                ActivityJournalLineCount = journalStatus.LineCount,
                StatusOk = exitOk,
                Message = storePosture.OperatorSummary,
                NextAction = nextAction.Action,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(exitOk ? 0 : 1, [json]);
        }

        return new OperatorCommandResult(exitOk ? 0 : 1, [
            "CARVES gateway activity status",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store operational posture: {storePosture.OperationalPosture}",
            $"Activity store operator summary: {storePosture.OperatorSummary}",
            $"Activity store issue count: {storePosture.IssueCount}",
            $"Activity store issues: {FormatActivityIssues(storePosture.Issues)}",
            $"Activity store recommended action: {storePosture.RecommendedAction}",
            $"Activity store next action: {nextAction.Action}",
            $"Activity store next action source: {nextAction.Source}",
            $"Activity store next action priority: {nextAction.Priority}",
            $"Activity store next action reason: {nextAction.Reason}",
            $"Activity store maturity stage: {maturity.Stage}",
            $"Activity store maturity ready: {maturity.Ready}",
            $"Activity store maturity posture: {maturity.Posture}",
            $"Activity store maturity summary: {maturity.Summary}",
            $"Activity store maturity limitations: {string.Join(", ", maturity.Limitations)}",
            $"Activity store operational ok: {operationalOk}",
            $"Activity store maturity required: {requiresMaturity}",
            $"Activity store exit policy: {exitPolicy}",
            $"Activity store exit ok: {exitOk}",
            $"Activity store exit reason: {exitReason}",
            $"Activity store writer lock status: {FormatActivityValue(journalStatus.LockStatus)}",
            $"Activity store writer lock currently held: {journalStatus.LockCurrentlyHeld}",
            $"Activity store dropped activity count: {journalStatus.DroppedActivityCount}",
            $"Activity store maintenance summary exists: {journalStatus.MaintenanceSummaryExists}",
            $"Activity store last maintenance UTC: {FormatActivityValue(journalStatus.LastMaintenanceAtUtc)}",
            $"Activity store last maintenance operation: {FormatActivityValue(journalStatus.LastMaintenanceOperation)}",
            $"Activity store last maintenance applied: {journalStatus.LastMaintenanceApplied}",
            $"Activity store last maintenance reason: {FormatActivityValue(journalStatus.LastMaintenanceReason)}",
            $"Activity store last maintenance recommended action: {FormatActivityValue(journalStatus.LastMaintenanceRecommendedAction)}",
            $"Activity store maintenance freshness posture: {maintenanceFreshness.Posture}",
            $"Activity store maintenance freshness age minutes: {maintenanceFreshness.AgeMinutes}",
            $"Activity store maintenance freshness recommended action: {maintenanceFreshness.RecommendedAction}",
            $"Activity store verification summary exists: {journalStatus.VerificationSummaryExists}",
            $"Activity store last verification UTC: {FormatActivityValue(journalStatus.LastVerificationAtUtc)}",
            $"Activity store last verification passed: {journalStatus.LastVerificationPassed}",
            $"Activity store last verification posture: {FormatActivityValue(journalStatus.LastVerificationPosture)}",
            $"Activity store last verification issues: {journalStatus.LastVerificationIssueCount}",
            $"Activity store last verification recommended action: {FormatActivityValue(journalStatus.LastVerificationRecommendedAction)}",
            $"Activity store last verification manifest generated UTC: {FormatActivityValue(journalStatus.LastVerificationManifestGeneratedAtUtc)}",
            $"Activity store last verification manifest sha256: {FormatActivityValue(journalStatus.LastVerificationManifestSha256)}",
            $"Activity store last verification checkpoint sequence: {journalStatus.LastVerificationCheckpointLatestSequence}",
            $"Activity store verification freshness posture: {verificationFreshness.Posture}",
            $"Activity store verification freshness age minutes: {verificationFreshness.AgeMinutes}",
            $"Activity store verification freshness recommended action: {verificationFreshness.RecommendedAction}",
            $"Activity store verification current proof: {verificationFreshness.CurrentProof}",
            $"Activity store verification current proof reason: {verificationFreshness.CurrentProofReason}",
            $"Activity journal storage mode: {journalStatus.StorageMode}",
            $"Activity journal exists: {journalStatus.Exists}",
            $"Activity journal archive directory: {FormatLogPath(journalStatus.ArchiveDirectory)}",
            $"Activity journal archive segment count: {journalStatus.ArchiveSegmentCount}",
            $"Activity journal archive bytes: {journalStatus.ArchiveByteCount}",
            $"Activity journal segment count: {journalStatus.SegmentCount}",
            $"Activity journal manifest exists: {journalStatus.ManifestExists}",
            $"Activity journal manifest generated UTC: {FormatActivityValue(journalStatus.ManifestGeneratedAtUtc)}",
            $"Activity journal manifest records: {journalStatus.ManifestRecordCount}",
            $"Activity journal manifest bytes: {journalStatus.ManifestByteCount}",
            $"Activity journal manifest first UTC: {FormatActivityValue(journalStatus.ManifestFirstTimestampUtc)}",
            $"Activity journal manifest last UTC: {FormatActivityValue(journalStatus.ManifestLastTimestampUtc)}",
            $"Activity journal checkpoint chain exists: {journalStatus.CheckpointChainExists}",
            $"Activity journal legacy fallback used: {journalStatus.LegacyFallbackUsed}",
            $"Status ok: {exitOk}",
            $"Next action: {nextAction.Action}",
        ]);
    }

    private static OperatorCommandResult GatewayActivityExplain(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var requestId = ResolveGatewayActivityExplainRequestId(commandLine.Arguments);
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return OperatorCommandResult.Failure(
                "Gateway activity explain requires a request id.",
                "Usage: carves gateway activity explain <request-id> [--json]");
        }

        var discovery = new LocalHostDiscoveryService().Discover(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var journalStatus = GatewayActivityJournal.Inspect(activityJournalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var entries = ReadGatewayActivityJournalByRequestId(activityJournalPath, requestId);
        var found = entries.Count > 0;
        var first = entries.FirstOrDefault();
        var last = entries.LastOrDefault();
        var durationMs = first is not null && last is not null
            ? (long)Math.Max(0, (last.Timestamp - first.Timestamp).TotalMilliseconds)
            : 0;
        var command = ResolveFirstActivityField(entries, "command");
        var method = ResolveFirstActivityField(entries, "method");
        var path = ResolveFirstActivityField(entries, "path");
        var status = ResolveLastActivityField(entries, "status");
        var exitCode = ResolveLastActivityField(entries, "exit_code");
        var sessionId = ResolveFirstActivityField(entries, "session_id");
        var messageId = ResolveFirstActivityField(entries, "message_id");
        var operationId = ResolveLastActivityField(entries, "operation_id");
        var requestedAction = ResolveLastActivityField(entries, "requested_action");
        var operationState = ResolveLastActivityField(entries, "operation_state");
        var message = found
            ? "Gateway activity request chain was found in the activity journal."
            : "Gateway activity request id was not found in active, archived, or legacy activity journal records.";
        var nextAction = BuildGatewayActivityExplainNextAction(found, requestId);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity-explain.v6",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreManifestSchemaVersion = GatewayActivityJournal.ManifestSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = storePosture.OperationalPosture,
                ActivityStoreOperatorSummary = storePosture.OperatorSummary,
                ActivityStoreIssueCount = storePosture.IssueCount,
                ActivityStoreIssues = storePosture.Issues,
                ActivityStoreRecommendedAction = storePosture.RecommendedAction,
                ActivityStoreRetentionMode = journalStatus.RetentionMode,
                ActivityStoreRetentionExecutionMode = journalStatus.RetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = journalStatus.DefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = journalStatus.WriterLockMode,
                ActivityStoreWriterLockPath = journalStatus.LockPath,
                ActivityStoreWriterLockExists = journalStatus.LockExists,
                ActivityStoreWriterLockFileExists = journalStatus.LockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = journalStatus.LockCurrentlyHeld,
                ActivityStoreWriterLockStatus = journalStatus.LockStatus,
                ActivityStoreWriterLockLastHolderProcessId = journalStatus.LockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = journalStatus.LockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = journalStatus.LockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = journalStatus.IntegrityMode,
                ActivitySearchScope = "active_and_archived_segments_with_legacy_fallback",
                ActivityJournalPath = journalStatus.Path,
                ActivityJournalStorageMode = journalStatus.StorageMode,
                ActivityJournalSegmentDirectory = journalStatus.SegmentDirectory,
                ActivityJournalArchiveDirectory = journalStatus.ArchiveDirectory,
                RequestId = requestId,
                Found = found,
                ExplanationPosture = found ? "found" : "not_found",
                EntryCount = entries.Count,
                RequestEntryCount = entries.Count(entry => GatewayActivityEventKinds.IsRequest(entry.Event)),
                FeedbackEntryCount = entries.Count(entry => GatewayActivityEventKinds.IsFeedback(entry.Event)),
                GatewayRequestEntryCount = entries.Count(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryGatewayRequest)),
                RouteNotFoundEntryCount = entries.Count(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryRouteNotFound)),
                FirstActivityAtUtc = first?.Timestamp.ToString("O") ?? string.Empty,
                LastActivityAtUtc = last?.Timestamp.ToString("O") ?? string.Empty,
                DurationMs = durationMs,
                Command = command,
                Method = method,
                Path = path,
                Status = status,
                ExitCode = exitCode,
                SessionId = sessionId,
                MessageId = messageId,
                OperationId = operationId,
                RequestedAction = requestedAction,
                OperationState = operationState,
                Entries = entries,
                Message = message,
                NextAction = nextAction.Action,
                NextActionSource = nextAction.Source,
                NextActionPriority = nextAction.Priority,
                NextActionReason = nextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(found ? 0 : 1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES gateway activity explain",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store not for: {string.Join(", ", GatewayActivityEventKinds.StoreNotFor)}",
            $"Activity store operational posture: {storePosture.OperationalPosture}",
            $"Activity store operator summary: {storePosture.OperatorSummary}",
            $"Activity store issue count: {storePosture.IssueCount}",
            $"Activity store issues: {FormatActivityIssues(storePosture.Issues)}",
            $"Activity store recommended action: {storePosture.RecommendedAction}",
            $"Activity store retention mode: {journalStatus.RetentionMode}",
            $"Activity store retention execution mode: {journalStatus.RetentionExecutionMode}",
            $"Activity store writer lock mode: {journalStatus.WriterLockMode}",
            $"Activity store writer lock: {FormatLogPath(journalStatus.LockPath)}",
            $"Activity store writer lock file exists: {journalStatus.LockFileExists}",
            $"Activity store writer lock currently held: {journalStatus.LockCurrentlyHeld}",
            $"Activity store writer lock status: {FormatActivityValue(journalStatus.LockStatus)}",
            $"Activity store writer lock last holder pid: {FormatActivityValue(journalStatus.LockLastHolderProcessId)}",
            $"Activity store writer lock last acquired UTC: {FormatActivityValue(journalStatus.LockLastAcquiredAtUtc)}",
            $"Activity store integrity mode: {journalStatus.IntegrityMode}",
            "Activity search scope: active_and_archived_segments_with_legacy_fallback",
            $"Activity journal: {FormatLogPath(journalStatus.Path)}",
            $"Activity journal storage mode: {journalStatus.StorageMode}",
            $"Activity journal segment directory: {FormatLogPath(journalStatus.SegmentDirectory)}",
            $"Activity journal archive directory: {FormatLogPath(journalStatus.ArchiveDirectory)}",
            $"Request id: {requestId}",
            $"Found: {found}",
            $"Entry count: {entries.Count}",
            $"Request entries: {entries.Count(entry => GatewayActivityEventKinds.IsRequest(entry.Event))}",
            $"Feedback entries: {entries.Count(entry => GatewayActivityEventKinds.IsFeedback(entry.Event))}",
            $"Gateway request entries: {entries.Count(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryGatewayRequest))}",
            $"Route not found entries: {entries.Count(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryRouteNotFound))}",
            $"First activity UTC: {FormatActivityValue(first?.Timestamp.ToString("O") ?? string.Empty)}",
            $"Last activity UTC: {FormatActivityValue(last?.Timestamp.ToString("O") ?? string.Empty)}",
            $"Duration ms: {durationMs}",
            $"Command: {FormatActivityValue(command)}",
            $"Method: {FormatActivityValue(method)}",
            $"Path: {FormatActivityValue(path)}",
            $"Status: {FormatActivityValue(status)}",
            $"Exit code: {FormatActivityValue(exitCode)}",
            $"Session id: {FormatActivityValue(sessionId)}",
            $"Message id: {FormatActivityValue(messageId)}",
            $"Operation id: {FormatActivityValue(operationId)}",
            $"Requested action: {FormatActivityValue(requestedAction)}",
            $"Operation state: {FormatActivityValue(operationState)}",
            $"Message: {message}",
        };

        if (entries.Count == 0)
        {
            lines.Add("Entries: none");
        }
        else
        {
            lines.Add("Entries:");
            foreach (var entry in entries)
            {
                lines.Add($"  {entry.Timestamp:O} {entry.Event} {FormatGatewayActivityEntryFields(entry)}");
            }
        }

        lines.Add($"Next action: {nextAction.Action}");
        lines.Add($"Next action source: {nextAction.Source}");
        lines.Add($"Next action priority: {nextAction.Priority}");
        lines.Add($"Next action reason: {nextAction.Reason}");
        return new OperatorCommandResult(found ? 0 : 1, lines);
    }

    private static OperatorCommandResult GatewayActivityVerify(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var discovery = new LocalHostDiscoveryService().InspectCached(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var verification = GatewayActivityJournal.Verify(activityJournalPath);
        var journalStatus = GatewayActivityJournal.Inspect(activityJournalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var message = verification.PossiblyTransient
            ? "Gateway activity verify could not acquire the activity store lock; retry after the current writer finishes."
            : verification.Passed
                ? "Gateway activity segment manifest and checkpoint chain match the current segment files."
                : "Gateway activity segment manifest or checkpoint chain does not match the current segment files.";
        var nextAction = BuildGatewayActivityVerifyNextAction(verification);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity-verify.v8",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreManifestSchemaVersion = GatewayActivityJournal.ManifestSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = storePosture.OperationalPosture,
                ActivityStoreOperatorSummary = storePosture.OperatorSummary,
                ActivityStoreIssueCount = storePosture.IssueCount,
                ActivityStoreIssues = storePosture.Issues,
                ActivityStoreRecommendedAction = storePosture.RecommendedAction,
                ActivityStoreRetentionMode = journalStatus.RetentionMode,
                ActivityStoreRetentionExecutionMode = journalStatus.RetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = journalStatus.DefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = journalStatus.WriterLockMode,
                ActivityStoreWriterLockPath = journalStatus.LockPath,
                ActivityStoreWriterLockExists = journalStatus.LockExists,
                ActivityStoreWriterLockFileExists = journalStatus.LockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = journalStatus.LockCurrentlyHeld,
                ActivityStoreWriterLockStatus = journalStatus.LockStatus,
                ActivityStoreWriterLockLastHolderProcessId = journalStatus.LockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = journalStatus.LockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = journalStatus.LockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = journalStatus.IntegrityMode,
                ActivityStoreVerificationSummarySchemaVersion = GatewayActivityJournal.VerificationSummarySchemaVersion,
                ActivityStoreVerificationSummaryPath = journalStatus.VerificationSummaryPath,
                ActivityStoreVerificationSummaryExists = journalStatus.VerificationSummaryExists,
                ActivityStoreLastVerificationAtUtc = journalStatus.LastVerificationAtUtc,
                ActivityStoreLastVerificationPassed = journalStatus.LastVerificationPassed,
                ActivityStoreLastVerificationPosture = journalStatus.LastVerificationPosture,
                ActivityStoreLastVerificationPossiblyTransient = journalStatus.LastVerificationPossiblyTransient,
                ActivityStoreLastVerificationConsistencyMode = journalStatus.LastVerificationConsistencyMode,
                ActivityStoreLastVerificationSnapshotLockAcquired = journalStatus.LastVerificationSnapshotLockAcquired,
                ActivityStoreLastVerificationIssueCount = journalStatus.LastVerificationIssueCount,
                ActivityStoreLastVerificationStoredManifestRecordCount = journalStatus.LastVerificationStoredManifestRecordCount,
                ActivityStoreLastVerificationActualManifestRecordCount = journalStatus.LastVerificationActualManifestRecordCount,
                ActivityStoreLastVerificationStoredManifestByteCount = journalStatus.LastVerificationStoredManifestByteCount,
                ActivityStoreLastVerificationActualManifestByteCount = journalStatus.LastVerificationActualManifestByteCount,
                ActivityStoreLastVerificationRecommendedAction = journalStatus.LastVerificationRecommendedAction,
                ActivityStoreLastVerificationManifestGeneratedAtUtc = journalStatus.LastVerificationManifestGeneratedAtUtc,
                ActivityStoreLastVerificationManifestSha256 = journalStatus.LastVerificationManifestSha256,
                ActivityStoreLastVerificationCheckpointLatestSequence = journalStatus.LastVerificationCheckpointLatestSequence,
                ActivityStoreLastVerificationCheckpointLatestCheckpointSha256 = journalStatus.LastVerificationCheckpointLatestCheckpointSha256,
                ActivityStoreLastVerificationCheckpointLatestManifestSha256 = journalStatus.LastVerificationCheckpointLatestManifestSha256,
                ActivityStoreVerifyConsistencyMode = verification.ConsistencyMode,
                ActivityStoreVerifySnapshotLockAcquired = verification.SnapshotLockAcquired,
                ActivityStoreVerifySnapshotLockStatus = verification.SnapshotLockStatus,
                ActivityStoreVerifySnapshotLockPath = verification.SnapshotLockPath,
                ActivityStoreVerifySnapshotLockAcquireTimeoutMs = verification.SnapshotLockAcquireTimeoutMs,
                VerificationPassed = verification.Passed,
                VerificationPosture = verification.Posture,
                VerificationPossiblyTransient = verification.PossiblyTransient,
                Message = message,
                JournalPath = verification.JournalPath,
                ManifestPath = verification.ManifestPath,
                SegmentDirectory = verification.SegmentDirectory,
                CheckpointChainPath = verification.CheckpointChainPath,
                CheckpointChainExists = verification.CheckpointChainExists,
                CheckpointChainCount = verification.CheckpointChainCount,
                CheckpointChainLatestSequence = verification.CheckpointChainLatestSequence,
                CheckpointChainLatestCheckpointSha256 = verification.CheckpointChainLatestCheckpointSha256,
                CheckpointChainLatestManifestSha256 = verification.CheckpointChainLatestManifestSha256,
                StoredManifest = verification.StoredManifest,
                ActualManifest = verification.ActualManifest,
                IssueCount = verification.Issues.Count,
                Issues = verification.Issues,
                NextAction = nextAction.Action,
                NextActionSource = nextAction.Source,
                NextActionPriority = nextAction.Priority,
                NextActionReason = nextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(verification.Passed ? 0 : 1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES gateway activity verify",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store not for: {string.Join(", ", GatewayActivityEventKinds.StoreNotFor)}",
            $"Activity store operational posture: {storePosture.OperationalPosture}",
            $"Activity store operator summary: {storePosture.OperatorSummary}",
            $"Activity store issue count: {storePosture.IssueCount}",
            $"Activity store issues: {FormatActivityIssues(storePosture.Issues)}",
            $"Activity store recommended action: {storePosture.RecommendedAction}",
            $"Activity store schema: {GatewayActivityEventKinds.StoreSchemaPath}",
            $"Activity store manifest schema: {GatewayActivityEventKinds.StoreManifestSchemaPath}",
            $"Activity store checkpoint schema: {GatewayActivityEventKinds.StoreCheckpointSchemaPath}",
            $"Activity store retention mode: {journalStatus.RetentionMode}",
            $"Activity store retention execution mode: {journalStatus.RetentionExecutionMode}",
            $"Activity store default archive before days: {journalStatus.DefaultArchiveBeforeDays}",
            $"Activity store writer lock mode: {journalStatus.WriterLockMode}",
            $"Activity store writer lock: {FormatLogPath(journalStatus.LockPath)}",
            $"Activity store writer lock file exists: {journalStatus.LockFileExists}",
            $"Activity store writer lock currently held: {journalStatus.LockCurrentlyHeld}",
            $"Activity store writer lock status: {FormatActivityValue(journalStatus.LockStatus)}",
            $"Activity store writer lock last holder pid: {FormatActivityValue(journalStatus.LockLastHolderProcessId)}",
            $"Activity store writer lock last acquired UTC: {FormatActivityValue(journalStatus.LockLastAcquiredAtUtc)}",
            $"Activity store writer lock timeout ms: {journalStatus.LockAcquireTimeoutMs}",
            $"Activity store integrity mode: {journalStatus.IntegrityMode}",
            $"Activity store verification summary exists: {journalStatus.VerificationSummaryExists}",
            $"Activity store last verification UTC: {FormatActivityValue(journalStatus.LastVerificationAtUtc)}",
            $"Activity store last verification passed: {journalStatus.LastVerificationPassed}",
            $"Activity store last verification posture: {FormatActivityValue(journalStatus.LastVerificationPosture)}",
            $"Activity store last verification issues: {journalStatus.LastVerificationIssueCount}",
            $"Activity store last verification recommended action: {FormatActivityValue(journalStatus.LastVerificationRecommendedAction)}",
            $"Activity store verify consistency mode: {verification.ConsistencyMode}",
            $"Activity store verify snapshot lock acquired: {verification.SnapshotLockAcquired}",
            $"Activity store verify snapshot lock status: {verification.SnapshotLockStatus}",
            $"Activity store verify snapshot lock: {FormatLogPath(verification.SnapshotLockPath)}",
            $"Activity store verify snapshot lock timeout ms: {verification.SnapshotLockAcquireTimeoutMs}",
            $"Verification passed: {verification.Passed}",
            $"Verification posture: {verification.Posture}",
            $"Verification possibly transient: {verification.PossiblyTransient}",
            $"Message: {message}",
            $"Journal path: {FormatLogPath(verification.JournalPath)}",
            $"Manifest path: {FormatLogPath(verification.ManifestPath)}",
            $"Segment directory: {FormatLogPath(verification.SegmentDirectory)}",
            $"Checkpoint chain path: {FormatLogPath(verification.CheckpointChainPath)}",
            $"Checkpoint chain exists: {verification.CheckpointChainExists}",
            $"Checkpoint count: {verification.CheckpointChainCount}",
            $"Checkpoint latest sequence: {verification.CheckpointChainLatestSequence}",
            $"Checkpoint latest hash: {FormatActivityValue(verification.CheckpointChainLatestCheckpointSha256)}",
            $"Checkpoint latest manifest hash: {FormatActivityValue(verification.CheckpointChainLatestManifestSha256)}",
            $"Stored manifest records: {verification.StoredManifest?.RecordCount ?? 0}",
            $"Actual manifest records: {verification.ActualManifest.RecordCount}",
            $"Stored manifest bytes: {verification.StoredManifest?.ByteCount ?? 0}",
            $"Actual manifest bytes: {verification.ActualManifest.ByteCount}",
            $"Issue count: {verification.Issues.Count}",
        };

        if (verification.Issues.Count == 0)
        {
            lines.Add("Issues: none");
        }
        else
        {
            lines.Add("Issues:");
            foreach (var issue in verification.Issues)
            {
                lines.Add($"  {issue.Code} path={FormatLogPath(issue.Path)} expected={FormatActivityValue(issue.Expected)} actual={FormatActivityValue(issue.Actual)}");
            }
        }

        lines.Add($"Next action: {nextAction.Action}");
        lines.Add($"Next action source: {nextAction.Source}");
        lines.Add($"Next action priority: {nextAction.Priority}");
        lines.Add($"Next action reason: {nextAction.Reason}");
        return new OperatorCommandResult(verification.Passed ? 0 : 1, lines);
    }

    private static OperatorCommandResult GatewayActivityArchive(CommandLineArguments commandLine)
    {
        var wantsJson = commandLine.Arguments.Any(argument =>
            string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
        var beforeDays = ResolveOptionalPositiveInt(
            commandLine.Arguments,
            "--before-days",
            GatewayActivityJournal.DefaultArchiveBeforeDays);
        var discovery = new LocalHostDiscoveryService().Discover(commandLine.RepoRoot);
        var activityJournalPath = ResolveGatewayActivityJournalPath(commandLine.RepoRoot, discovery);
        var archive = GatewayActivityJournal.Archive(activityJournalPath, beforeDays);
        var journalStatus = GatewayActivityJournal.Inspect(activityJournalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var succeeded = !string.Equals(archive.Reason, "segment_archive_partial_failed", StringComparison.Ordinal);
        var message = archive.Applied
            ? "Gateway activity segments were moved to the archive without deleting evidence."
            : archive.Reason switch
            {
                "no_archive_candidates" => "Gateway activity archive found no old segment candidates.",
                "segment_store_missing" => "Gateway activity segment store is not available yet.",
                _ => "Gateway activity archive did not complete cleanly.",
            };
        var nextAction = BuildGatewayActivityArchiveNextAction(archive, succeeded);

        if (wantsJson)
        {
            var payload = new
            {
                SchemaVersion = "carves-gateway-activity-archive.v6",
                RepoRoot = commandLine.RepoRoot,
                HostRunning = discovery.HostRunning,
                GatewayRole = "connection_routing_observability",
                GatewayAutomationBoundary = "no_worker_automation_dispatch",
                ActivityStoreContractVersion = GatewayActivityEventKinds.StoreContractVersion,
                ActivityStoreRecordSchemaVersion = GatewayActivityEventKinds.StoreRecordSchemaVersion,
                ActivityStoreManifestSchemaVersion = GatewayActivityJournal.ManifestSchemaVersion,
                ActivityStoreCheckpointSchemaVersion = GatewayActivityJournal.CheckpointSchemaVersion,
                ActivityStoreContractPath = GatewayActivityEventKinds.StoreContractPath,
                ActivityStoreSchemaPath = GatewayActivityEventKinds.StoreSchemaPath,
                ActivityStoreManifestSchemaPath = GatewayActivityEventKinds.StoreManifestSchemaPath,
                ActivityStoreCheckpointSchemaPath = GatewayActivityEventKinds.StoreCheckpointSchemaPath,
                ActivityStoreTruthBoundary = GatewayActivityEventKinds.StoreTruthBoundary,
                ActivityStoreAuthorityPosture = GatewayActivityEventKinds.StoreAuthorityPosture,
                ActivityStoreCompletenessPosture = GatewayActivityEventKinds.StoreCompletenessPosture,
                ActivityStoreAuditTruth = GatewayActivityEventKinds.StoreAuditTruth,
                ActivityStoreOperatorUse = GatewayActivityEventKinds.StoreOperatorUse,
                ActivityStoreNotFor = GatewayActivityEventKinds.StoreNotFor,
                ActivityStoreOperationalPosture = storePosture.OperationalPosture,
                ActivityStoreOperatorSummary = storePosture.OperatorSummary,
                ActivityStoreIssueCount = storePosture.IssueCount,
                ActivityStoreIssues = storePosture.Issues,
                ActivityStoreRecommendedAction = storePosture.RecommendedAction,
                ActivityStoreRetentionMode = journalStatus.RetentionMode,
                ActivityStoreRetentionExecutionMode = journalStatus.RetentionExecutionMode,
                ActivityStoreDefaultArchiveBeforeDays = journalStatus.DefaultArchiveBeforeDays,
                ActivityStoreWriterLockMode = journalStatus.WriterLockMode,
                ActivityStoreWriterLockPath = journalStatus.LockPath,
                ActivityStoreWriterLockExists = journalStatus.LockExists,
                ActivityStoreWriterLockFileExists = journalStatus.LockFileExists,
                ActivityStoreWriterLockCurrentlyHeld = journalStatus.LockCurrentlyHeld,
                ActivityStoreWriterLockStatus = journalStatus.LockStatus,
                ActivityStoreWriterLockLastHolderProcessId = journalStatus.LockLastHolderProcessId,
                ActivityStoreWriterLockLastAcquiredAtUtc = journalStatus.LockLastAcquiredAtUtc,
                ActivityStoreWriterLockAcquireTimeoutMs = journalStatus.LockAcquireTimeoutMs,
                ActivityStoreIntegrityMode = journalStatus.IntegrityMode,
                ActivityStoreMaintenanceSummarySchemaVersion = GatewayActivityJournal.MaintenanceSummarySchemaVersion,
                ActivityStoreMaintenanceSummaryPath = journalStatus.MaintenanceSummaryPath,
                ActivityStoreMaintenanceSummaryExists = journalStatus.MaintenanceSummaryExists,
                ActivityStoreLastMaintenanceAtUtc = journalStatus.LastMaintenanceAtUtc,
                ActivityStoreLastMaintenanceOperation = journalStatus.LastMaintenanceOperation,
                ActivityStoreLastMaintenanceDryRun = journalStatus.LastMaintenanceDryRun,
                ActivityStoreLastMaintenanceApplied = journalStatus.LastMaintenanceApplied,
                ActivityStoreLastMaintenanceReason = journalStatus.LastMaintenanceReason,
                ActivityStoreLastMaintenanceBeforeDays = journalStatus.LastMaintenanceBeforeDays,
                ActivityStoreLastMaintenanceArchiveBeforeUtcDate = journalStatus.LastMaintenanceArchiveBeforeUtcDate,
                ActivityStoreLastMaintenanceCandidateSegmentCount = journalStatus.LastMaintenanceCandidateSegmentCount,
                ActivityStoreLastMaintenanceArchivedSegmentCount = journalStatus.LastMaintenanceArchivedSegmentCount,
                ActivityStoreLastMaintenanceArchivedByteCount = journalStatus.LastMaintenanceArchivedByteCount,
                ActivityStoreLastMaintenanceArchivedRecordCount = journalStatus.LastMaintenanceArchivedRecordCount,
                ActivityStoreLastMaintenanceRecommendedAction = journalStatus.LastMaintenanceRecommendedAction,
                ArchiveMode = "non_destructive_segment_move",
                ArchiveRequested = archive.Requested,
                ArchiveApplied = archive.Applied,
                ArchiveReason = archive.Reason,
                ArchiveBeforeDays = archive.BeforeDays,
                ArchiveBeforeUtcDate = archive.ArchiveBeforeUtcDate,
                ArchiveDirectory = archive.ArchiveDirectory,
                CandidateSegmentCount = archive.CandidateSegmentCount,
                ArchivedSegmentCount = archive.ArchivedSegmentCount,
                ArchivedSegments = archive.ArchivedSegments,
                Message = message,
                NextAction = nextAction.Action,
                NextActionSource = nextAction.Source,
                NextActionPriority = nextAction.Priority,
                NextActionReason = nextAction.Reason,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            return new OperatorCommandResult(succeeded ? 0 : 1, [json]);
        }

        var lines = new List<string>
        {
            "CARVES gateway activity archive",
            $"Repo root: {commandLine.RepoRoot}",
            $"Host running: {discovery.HostRunning}",
            "Gateway role: connection, routing, and observability only",
            "Gateway automation boundary: worker automation is controlled separately by role-mode gates",
            $"Activity store contract: {GatewayActivityEventKinds.StoreContractVersion}",
            $"Activity store truth boundary: {GatewayActivityEventKinds.StoreTruthBoundary}",
            $"Activity store authority posture: {GatewayActivityEventKinds.StoreAuthorityPosture}",
            $"Activity store completeness posture: {GatewayActivityEventKinds.StoreCompletenessPosture}",
            $"Activity store audit truth: {GatewayActivityEventKinds.StoreAuditTruth}",
            $"Activity store operator use: {GatewayActivityEventKinds.StoreOperatorUse}",
            $"Activity store not for: {string.Join(", ", GatewayActivityEventKinds.StoreNotFor)}",
            $"Activity store operational posture: {storePosture.OperationalPosture}",
            $"Activity store operator summary: {storePosture.OperatorSummary}",
            $"Activity store issue count: {storePosture.IssueCount}",
            $"Activity store issues: {FormatActivityIssues(storePosture.Issues)}",
            $"Activity store recommended action: {storePosture.RecommendedAction}",
            $"Activity store retention mode: {journalStatus.RetentionMode}",
            $"Activity store retention execution mode: {journalStatus.RetentionExecutionMode}",
            $"Activity store default archive before days: {journalStatus.DefaultArchiveBeforeDays}",
            $"Activity store writer lock mode: {journalStatus.WriterLockMode}",
            $"Activity store writer lock: {FormatLogPath(journalStatus.LockPath)}",
            $"Activity store writer lock file exists: {journalStatus.LockFileExists}",
            $"Activity store writer lock currently held: {journalStatus.LockCurrentlyHeld}",
            $"Activity store writer lock status: {FormatActivityValue(journalStatus.LockStatus)}",
            $"Activity store writer lock last holder pid: {FormatActivityValue(journalStatus.LockLastHolderProcessId)}",
            $"Activity store writer lock last acquired UTC: {FormatActivityValue(journalStatus.LockLastAcquiredAtUtc)}",
            $"Activity store writer lock timeout ms: {journalStatus.LockAcquireTimeoutMs}",
            $"Activity store integrity mode: {journalStatus.IntegrityMode}",
            $"Activity store maintenance summary exists: {journalStatus.MaintenanceSummaryExists}",
            $"Activity store last maintenance UTC: {FormatActivityValue(journalStatus.LastMaintenanceAtUtc)}",
            $"Activity store last maintenance operation: {FormatActivityValue(journalStatus.LastMaintenanceOperation)}",
            $"Activity store last maintenance applied: {journalStatus.LastMaintenanceApplied}",
            $"Activity store last maintenance reason: {FormatActivityValue(journalStatus.LastMaintenanceReason)}",
            $"Activity store last maintenance recommended action: {FormatActivityValue(journalStatus.LastMaintenanceRecommendedAction)}",
            $"Archive mode: non_destructive_segment_move",
            $"Archive requested: {archive.Requested}",
            $"Archive applied: {archive.Applied}",
            $"Archive reason: {archive.Reason}",
            $"Archive before days: {archive.BeforeDays}",
            $"Archive before UTC date: {archive.ArchiveBeforeUtcDate}",
            $"Archive directory: {FormatLogPath(archive.ArchiveDirectory)}",
            $"Candidate segment count: {archive.CandidateSegmentCount}",
            $"Archived segment count: {archive.ArchivedSegmentCount}",
            $"Message: {message}",
        };

        if (archive.ArchivedSegments.Count == 0)
        {
            lines.Add("Archived segments: none");
        }
        else
        {
            lines.Add("Archived segments:");
            foreach (var segment in archive.ArchivedSegments)
            {
                lines.Add($"  {segment.FileName} date={segment.SegmentDateUtc} records={segment.RecordCount} bytes={segment.ByteCount} sha256={FormatActivityValue(segment.Sha256)} archive={FormatLogPath(segment.ArchivePath)}");
            }
        }

        lines.Add($"Next action: {nextAction.Action}");
        lines.Add($"Next action source: {nextAction.Source}");
        lines.Add($"Next action priority: {nextAction.Priority}");
        lines.Add($"Next action reason: {nextAction.Reason}");
        return new OperatorCommandResult(succeeded ? 0 : 1, lines);
    }

    private static GatewayActivityFilter ResolveGatewayActivityFilter(IReadOnlyList<string> arguments)
    {
        var category = ResolveOption(arguments, "--category")?.Trim().ToLowerInvariant()
                       ?? GatewayActivityEventKinds.CategoryAll;
        var eventKind = ResolveOption(arguments, "--event")?.Trim() ?? string.Empty;
        var command = ResolveOption(arguments, "--command")?.Trim() ?? string.Empty;
        var path = ResolveOption(arguments, "--path")?.Trim() ?? string.Empty;
        var requestId = ResolveOption(arguments, "--request-id")?.Trim() ?? string.Empty;
        var operationId = ResolveOption(arguments, "--operation-id")?.Trim() ?? string.Empty;
        var sessionId = ResolveOption(arguments, "--session-id")?.Trim() ?? string.Empty;
        var messageId = ResolveOption(arguments, "--message-id")?.Trim() ?? string.Empty;
        var sinceMinutes = ResolveOptionalPositiveInt(arguments, "--since-minutes", defaultValue: 0);
        var sinceUtc = sinceMinutes > 0
            ? DateTimeOffset.UtcNow.AddMinutes(-sinceMinutes)
            : (DateTimeOffset?)null;

        if (!GatewayActivityEventKinds.IsKnownCategory(category))
        {
            return GatewayActivityFilter.Invalid(
                $"Unknown gateway activity category '{category}'. Known categories: {string.Join(", ", GatewayActivityEventKinds.AllCategories)}.");
        }

        if (!string.IsNullOrWhiteSpace(eventKind) && !GatewayActivityEventKinds.IsKnown(eventKind))
        {
            return GatewayActivityFilter.Invalid(
                $"Unknown gateway activity event '{eventKind}'. Known events: {string.Join(", ", GatewayActivityEventKinds.All)}.");
        }

        return new GatewayActivityFilter(
            true,
            category,
            eventKind,
            command,
            path,
            requestId,
            operationId,
            sessionId,
            messageId,
            sinceMinutes,
            sinceUtc,
            string.Empty);
    }

    private static IReadOnlyList<GatewayActivityEntry> ApplyGatewayActivityFilter(
        IReadOnlyList<GatewayActivityEntry> entries,
        GatewayActivityFilter filter)
    {
        return entries
            .Where(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, filter.Category))
            .Where(entry => string.IsNullOrWhiteSpace(filter.EventKind)
                            || string.Equals(entry.Event, filter.EventKind, StringComparison.Ordinal))
            .Where(entry => MatchesActivityField(entry, "command", filter.Command, StringComparison.OrdinalIgnoreCase))
            .Where(entry => MatchesActivityField(entry, "path", filter.Path, StringComparison.Ordinal))
            .Where(entry => MatchesActivityField(entry, "request_id", filter.RequestId, StringComparison.Ordinal))
            .Where(entry => MatchesActivityField(entry, "operation_id", filter.OperationId, StringComparison.Ordinal))
            .Where(entry => MatchesActivityField(entry, "session_id", filter.SessionId, StringComparison.Ordinal))
            .Where(entry => MatchesActivityField(entry, "message_id", filter.MessageId, StringComparison.Ordinal))
            .Where(entry => filter.SinceUtc is null || entry.Timestamp >= filter.SinceUtc.Value)
            .ToArray();
    }

    private static bool MatchesActivityField(
        GatewayActivityEntry entry,
        string key,
        string expectedValue,
        StringComparison comparison)
    {
        return string.IsNullOrWhiteSpace(expectedValue)
               || (entry.Fields.TryGetValue(key, out var value)
                   && string.Equals(value, expectedValue, comparison));
    }

    private static int CountGatewayActivityEntries(IReadOnlyList<GatewayActivityEntry> entries, string category)
    {
        return entries.Count(entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, category));
    }

    private static GatewayActivityProjection BuildGatewayActivityProjection(
        string repoRoot,
        HostDiscoveryResult discovery,
        GatewayLogProjection logs,
        int tailLineCount,
        DateTimeOffset? sinceUtc)
    {
        var journalPath = ResolveGatewayActivityJournalPath(repoRoot, discovery);
        var journalStatus = GatewayActivityJournal.Inspect(journalPath);
        var storePosture = BuildGatewayActivityStoreOperationalPosture(journalStatus);
        var verificationFreshness = BuildGatewayActivityVerificationFreshnessPosture(journalStatus);
        var maintenanceFreshness = BuildGatewayActivityMaintenanceFreshnessPosture(journalStatus);
        var maturity = BuildGatewayActivityMaturityPosture(storePosture, verificationFreshness, maintenanceFreshness);
        var journalExists = journalStatus.Exists;
        var journalEntries = sinceUtc is null
            ? ReadGatewayActivityJournal(journalPath, tailLineCount)
            : ReadGatewayActivityJournalSince(journalPath, sinceUtc.Value);
        var storeBackedSince = sinceUtc is not null && journalExists;
        var logEntries = journalEntries.Count == 0 && !storeBackedSince
            ? ParseGatewayActivity(logs.StandardOutputTail)
            : Array.Empty<GatewayActivityEntry>();
        var entries = journalEntries.Count > 0 ? journalEntries : logEntries;
        var activitySource = journalEntries.Count > 0 || storeBackedSince
            ? "activity_journal"
            : logEntries.Count > 0
                ? "stdout_log_tail"
                : "none";
        var activityQueryMode = storeBackedSince
            ? "store_backed_since"
            : "tail_window";
        var lastGatewayRequest = entries.LastOrDefault(static entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryGatewayRequest));
        var semanticEntries = entries
            .Where(static entry => !GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryGatewayRequest))
            .ToArray();
        var lastRequest = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsRequest(entry.Event));
        var lastFeedback = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsFeedback(entry.Event));
        var lastCliInvoke = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsCliInvoke(entry.Event));
        var lastRestRequest = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsRestRequest(entry.Event));
        var lastSessionMessage = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsSessionMessage(entry.Event));
        var lastOperationFeedback = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsOperationFeedback(entry.Event));
        var lastRouteNotFound = semanticEntries.LastOrDefault(static entry => GatewayActivityEventKinds.IsCategoryMatch(entry.Event, GatewayActivityEventKinds.CategoryRouteNotFound));
        return new GatewayActivityProjection(
            entries.Count > 0,
            entries.Count,
            semanticEntries.LastOrDefault() ?? entries.LastOrDefault(),
            lastRequest,
            lastFeedback,
            lastCliInvoke,
            lastRestRequest,
            lastSessionMessage,
            lastOperationFeedback,
            lastRouteNotFound,
            lastGatewayRequest,
            activitySource,
            activityQueryMode,
            storeBackedSince,
            !storeBackedSince,
            journalStatus.Path,
            journalStatus.StorageMode,
            journalStatus.StoreDirectory,
            journalStatus.SegmentDirectory,
            journalStatus.ArchiveDirectory,
            storePosture.OperationalPosture,
            storePosture.OperatorSummary,
            storePosture.IssueCount,
            storePosture.Issues,
            storePosture.RecommendedAction,
            verificationFreshness.Mode,
            verificationFreshness.ThresholdHours,
            verificationFreshness.Posture,
            verificationFreshness.AgeMinutes,
            verificationFreshness.RecommendedAction,
            verificationFreshness.CurrentProof,
            verificationFreshness.CurrentProofReason,
            maintenanceFreshness.Mode,
            maintenanceFreshness.ThresholdDays,
            maintenanceFreshness.Posture,
            maintenanceFreshness.AgeMinutes,
            maintenanceFreshness.RecommendedAction,
            maturity.Stage,
            maturity.Ready,
            maturity.Posture,
            maturity.Summary,
            maturity.Limitations,
            journalStatus.RetentionMode,
            journalStatus.RetentionExecutionMode,
            journalStatus.DefaultArchiveBeforeDays,
            journalStatus.WriterLockMode,
            journalStatus.LockPath,
            journalStatus.LockExists,
            journalStatus.LockFileExists,
            journalStatus.LockCurrentlyHeld,
            journalStatus.LockStatus,
            journalStatus.LockLastHolderProcessId,
            journalStatus.LockLastAcquiredAtUtc,
            journalStatus.LockAcquireTimeoutMs,
            journalStatus.IntegrityMode,
            journalStatus.DropTelemetryPath,
            journalStatus.DropTelemetryExists,
            journalStatus.DroppedActivityCount,
            journalStatus.LastDropAtUtc,
            journalStatus.LastDropReason,
            journalStatus.LastDropEvent,
            journalStatus.LastDropRequestId,
            journalStatus.ArchiveSegmentCount,
            journalStatus.ArchiveByteCount,
            journalStatus.ActiveSegmentPath,
            journalStatus.SegmentCount,
            journalStatus.ManifestPath,
            journalStatus.ManifestExists,
            journalStatus.ManifestSchemaVersion,
            journalStatus.CheckpointChainPath,
            journalStatus.CheckpointChainExists,
            journalStatus.CheckpointChainCount,
            journalStatus.CheckpointChainLatestSequence,
            journalStatus.CheckpointChainLatestCheckpointSha256,
            journalStatus.CheckpointChainLatestManifestSha256,
            journalStatus.ManifestGeneratedAtUtc,
            journalStatus.ManifestRecordCount,
            journalStatus.ManifestByteCount,
            journalStatus.ManifestFirstTimestampUtc,
            journalStatus.ManifestLastTimestampUtc,
            journalStatus.Segments,
            journalStatus.LegacyJournalPath,
            journalStatus.LegacyJournalExists,
            journalStatus.LegacyFallbackUsed,
            journalExists,
            journalStatus.ByteCount,
            journalStatus.LineCount,
            entries);
    }

    private static string ResolveGatewayActivityJournalPath(string repoRoot, HostDiscoveryResult discovery)
    {
        var runtimeDirectory = discovery.Summary?.RuntimeDirectory
                               ?? discovery.Descriptor?.RuntimeDirectory
                               ?? discovery.Snapshot?.RuntimeDirectory;
        return string.IsNullOrWhiteSpace(runtimeDirectory)
            ? LocalHostPaths.GetGatewayActivityJournalPath(repoRoot)
            : LocalHostPaths.GetGatewayActivityJournalPathFromRuntimeDirectory(runtimeDirectory);
    }

    private static IReadOnlyList<GatewayActivityEntry> ReadGatewayActivityJournal(string path, int tailLineCount)
    {
        return GatewayActivityJournal.ReadTail(path, tailLineCount)
            .Where(static record => GatewayActivityEventKinds.IsKnown(record.Event))
            .Select(static record => new GatewayActivityEntry(
                record.Timestamp,
                record.Event,
                new Dictionary<string, string>(record.Fields, StringComparer.Ordinal)))
            .ToArray();
    }

    private static IReadOnlyList<GatewayActivityEntry> ReadGatewayActivityJournalSince(string path, DateTimeOffset sinceUtc)
    {
        return GatewayActivityJournal.ReadSince(path, sinceUtc)
            .Where(static record => GatewayActivityEventKinds.IsKnown(record.Event))
            .Select(static record => new GatewayActivityEntry(
                record.Timestamp,
                record.Event,
                new Dictionary<string, string>(record.Fields, StringComparer.Ordinal)))
            .ToArray();
    }

    private static IReadOnlyList<GatewayActivityEntry> ReadGatewayActivityJournalByRequestId(string path, string requestId)
    {
        return GatewayActivityJournal.ReadByRequestId(path, requestId)
            .Where(static record => GatewayActivityEventKinds.IsKnown(record.Event))
            .Select(static record => new GatewayActivityEntry(
                record.Timestamp,
                record.Event,
                new Dictionary<string, string>(record.Fields, StringComparer.Ordinal)))
            .ToArray();
    }

    private static GatewayLogProjection BuildGatewayLogProjection(HostDiscoveryResult discovery, int tailLineCount)
    {
        var deploymentDirectory = discovery.Summary?.DeploymentDirectory
                                  ?? discovery.Descriptor?.DeploymentDirectory
                                  ?? discovery.Snapshot?.DeploymentDirectory
                                  ?? string.Empty;
        var stdoutLogPath = string.IsNullOrWhiteSpace(deploymentDirectory)
            ? string.Empty
            : LocalHostProcessLauncher.GetStandardOutputLogPath(deploymentDirectory);
        var stderrLogPath = string.IsNullOrWhiteSpace(deploymentDirectory)
            ? string.Empty
            : LocalHostProcessLauncher.GetStandardErrorLogPath(deploymentDirectory);
        var stdoutExists = !string.IsNullOrWhiteSpace(stdoutLogPath) && File.Exists(stdoutLogPath);
        var stderrExists = !string.IsNullOrWhiteSpace(stderrLogPath) && File.Exists(stderrLogPath);
        return new GatewayLogProjection(
            deploymentDirectory,
            stdoutLogPath,
            stderrLogPath,
            stdoutExists,
            stderrExists,
            stdoutExists || stderrExists,
            tailLineCount,
            ReadLogTail(stdoutLogPath, tailLineCount),
            ReadLogTail(stderrLogPath, tailLineCount));
    }

    private static IReadOnlyList<GatewayActivityEntry> ParseGatewayActivity(IReadOnlyList<string> stdoutTail)
    {
        return stdoutTail
            .Select(ParseGatewayActivityLine)
            .Where(static entry => entry is not null)
            .Cast<GatewayActivityEntry>()
            .ToArray();
    }

    private static GatewayActivityEntry? ParseGatewayActivityLine(string line)
    {
        const string prefix = "[carves-gateway] ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var remainder = line[prefix.Length..];
        var firstSpace = remainder.IndexOf(' ', StringComparison.Ordinal);
        if (firstSpace <= 0 || !DateTimeOffset.TryParse(remainder[..firstSpace], out var timestamp))
        {
            return null;
        }

        var afterTimestamp = remainder[(firstSpace + 1)..];
        var eventEnd = afterTimestamp.IndexOf(' ', StringComparison.Ordinal);
        var eventName = eventEnd < 0 ? afterTimestamp : afterTimestamp[..eventEnd];
        if (!GatewayActivityEventKinds.IsKnown(eventName))
        {
            return null;
        }

        var fields = eventEnd < 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : ParseGatewayActivityFields(afterTimestamp[(eventEnd + 1)..]);
        return new GatewayActivityEntry(timestamp, eventName, fields);
    }

    private static IReadOnlyDictionary<string, string> ParseGatewayActivityFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length)
            {
                break;
            }

            var keyStart = index;
            while (index < text.Length && text[index] != '=' && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length || text[index] != '=')
            {
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                continue;
            }

            var key = text[keyStart..index];
            index++;
            string value;
            if (index < text.Length && text[index] == '"')
            {
                index++;
                var builder = new System.Text.StringBuilder();
                while (index < text.Length)
                {
                    var character = text[index++];
                    if (character == '\\' && index < text.Length)
                    {
                        builder.Append(text[index++]);
                        continue;
                    }

                    if (character == '"')
                    {
                        break;
                    }

                    builder.Append(character);
                }

                value = builder.ToString();
            }
            else
            {
                var valueStart = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                value = text[valueStart..index];
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                fields[key] = value;
            }
        }

        return fields;
    }

    private static GatewayActivityDiagnosticNextAction BuildGatewayActivityNextAction(
        GatewayLogProjection logs,
        GatewayActivityProjection activity)
    {
        if (activity.ActivityStoreIssueCount > 0)
        {
            return new GatewayActivityDiagnosticNextAction(
                activity.ActivityStoreRecommendedAction,
                "store_operational_posture",
                1,
                activity.ActivityStoreOperationalPosture);
        }

        if (!logs.LogsAvailable)
        {
            return activity.ActivityJournalExists
                ? new GatewayActivityDiagnosticNextAction(
                    "gateway activity visible",
                    "activity_journal",
                    0,
                    "activity_journal_exists_without_logs")
                : new GatewayActivityDiagnosticNextAction(
                    "carves gateway start",
                    "gateway_logs",
                    2,
                    "logs_and_activity_journal_missing");
        }

        return activity.ActivityAvailable
            ? new GatewayActivityDiagnosticNextAction(
                "gateway activity visible",
                "activity_visibility",
                0,
                "activity_entries_available")
            : new GatewayActivityDiagnosticNextAction(
                "carves gateway activity --tail 200",
                "activity_query",
                3,
                "no_known_activity_in_tail");
    }

    private static GatewayActivityStoreOperationalPosture BuildGatewayActivityStoreOperationalPosture(
        GatewayActivityJournalStatus status)
    {
        var issues = new List<string>();
        if (status.LockCurrentlyHeld)
        {
            issues.Add("writer_lock_currently_held");
        }

        if (status.DroppedActivityCount > 0)
        {
            issues.Add("activity_write_drops_recorded");
        }

        if (!status.Exists && !status.LegacyJournalExists)
        {
            issues.Add("activity_store_not_initialized");
        }
        else
        {
            if (status.Exists && !status.ManifestExists)
            {
                issues.Add("segment_manifest_missing");
            }

            if (status.Exists && !status.CheckpointChainExists)
            {
                issues.Add("manifest_checkpoint_chain_missing");
            }

            if (status.LegacyFallbackUsed)
            {
                issues.Add("legacy_activity_journal_fallback_used");
            }
        }

        var posture = issues.FirstOrDefault() switch
        {
            "writer_lock_currently_held" => "writer_lock_held",
            "activity_write_drops_recorded" => "activity_loss_detected",
            "activity_store_not_initialized" => "store_not_initialized",
            "segment_manifest_missing" or "manifest_checkpoint_chain_missing" => "metadata_incomplete",
            "legacy_activity_journal_fallback_used" => "legacy_fallback",
            _ => "ready",
        };
        var summary = posture switch
        {
            "writer_lock_held" => "Activity store is readable but a writer currently holds the store lock.",
            "activity_loss_detected" => "Activity store is usable, but failed activity writes were recorded.",
            "store_not_initialized" => "Activity store has not recorded Gateway activity yet.",
            "metadata_incomplete" => "Activity store records exist, but manifest or checkpoint metadata is incomplete.",
            "legacy_fallback" => "Activity store is using the legacy journal fallback.",
            _ => "Activity store is ready for operator diagnosis.",
        };
        var recommendedAction = posture switch
        {
            "writer_lock_held" => "retry after the current activity writer releases activity.lock",
            "activity_loss_detected" => "inspect gateway doctor drop telemetry and gateway logs",
            "store_not_initialized" => "start gateway and make a gateway request",
            "metadata_incomplete" => "gateway activity verify",
            "legacy_fallback" => "make a new gateway request to initialize the segmented activity store",
            _ => "gateway activity verify",
        };

        return new GatewayActivityStoreOperationalPosture(
            posture,
            summary,
            issues.Count,
            issues,
            recommendedAction);
    }

    private static GatewayActivityVerificationFreshnessPosture BuildGatewayActivityVerificationFreshnessPosture(
        GatewayActivityJournalStatus status)
    {
        const string mode = "latest_completed_verification_summary";
        var thresholdHours = (int)Math.Round(GatewayActivityVerificationFreshnessThreshold.TotalHours);

        if (!status.Exists && !status.LegacyJournalExists)
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                "not_applicable",
                -1,
                "start gateway and make a gateway request",
                false,
                "activity_store_not_initialized");
        }

        if (!status.VerificationSummaryExists || string.IsNullOrWhiteSpace(status.LastVerificationAtUtc))
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                "verification_missing",
                -1,
                "gateway activity verify",
                false,
                "verification_summary_missing");
        }

        if (!DateTimeOffset.TryParse(status.LastVerificationAtUtc, out var lastVerificationAt))
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                "verification_unknown",
                -1,
                "gateway activity verify",
                false,
                "verification_summary_timestamp_invalid");
        }

        var age = DateTimeOffset.UtcNow - lastVerificationAt.ToUniversalTime();
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var ageMinutes = (long)Math.Floor(age.TotalMinutes);
        if (!status.LastVerificationPassed
            || !string.Equals(status.LastVerificationPosture, "verified", StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                "verification_failed",
                ageMinutes,
                string.IsNullOrWhiteSpace(status.LastVerificationRecommendedAction)
                    ? "gateway activity verify"
                    : status.LastVerificationRecommendedAction,
                false,
                "last_verification_not_passed");
        }

        var currentProof = BuildGatewayActivityVerificationCurrentProof(status);
        if (!currentProof.CurrentProof)
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                string.Equals(
                    currentProof.Reason,
                    "verification_summary_missing_current_proof",
                    StringComparison.Ordinal)
                    ? "verification_currentness_unknown"
                    : "verification_stale_store_changed",
                ageMinutes,
                "gateway activity verify",
                false,
                currentProof.Reason);
        }

        if (age > GatewayActivityVerificationFreshnessThreshold)
        {
            return new GatewayActivityVerificationFreshnessPosture(
                mode,
                thresholdHours,
                "verification_stale",
                ageMinutes,
                "gateway activity verify",
                true,
                currentProof.Reason);
        }

        return new GatewayActivityVerificationFreshnessPosture(
            mode,
            thresholdHours,
            "verification_fresh",
            ageMinutes,
            "no verification action needed",
            true,
            currentProof.Reason);
    }

    private static GatewayActivityVerificationCurrentProof BuildGatewayActivityVerificationCurrentProof(
        GatewayActivityJournalStatus status)
    {
        if (string.IsNullOrWhiteSpace(status.LastVerificationManifestGeneratedAtUtc)
            || string.IsNullOrWhiteSpace(status.LastVerificationManifestSha256)
            || status.LastVerificationCheckpointLatestSequence <= 0
            || string.IsNullOrWhiteSpace(status.LastVerificationCheckpointLatestCheckpointSha256)
            || string.IsNullOrWhiteSpace(status.LastVerificationCheckpointLatestManifestSha256))
        {
            return new GatewayActivityVerificationCurrentProof(
                false,
                "verification_summary_missing_current_proof");
        }

        if (status.CheckpointChainLatestSequence <= 0
            || string.IsNullOrWhiteSpace(status.CheckpointChainLatestCheckpointSha256)
            || string.IsNullOrWhiteSpace(status.CheckpointChainLatestManifestSha256)
            || string.IsNullOrWhiteSpace(status.ManifestGeneratedAtUtc))
        {
            return new GatewayActivityVerificationCurrentProof(
                false,
                "current_manifest_fingerprint_missing");
        }

        if (status.LastVerificationCheckpointLatestSequence != status.CheckpointChainLatestSequence)
        {
            if (HasOnlyDiagnosticHandshakeActivitySinceLastVerification(status))
            {
                return new GatewayActivityVerificationCurrentProof(
                    true,
                    "verification_summary_matches_current_manifest");
            }

            return new GatewayActivityVerificationCurrentProof(
                false,
                "checkpoint_sequence_changed");
        }

        if (!string.Equals(
                status.LastVerificationCheckpointLatestCheckpointSha256,
                status.CheckpointChainLatestCheckpointSha256,
                StringComparison.Ordinal))
        {
            return new GatewayActivityVerificationCurrentProof(
                false,
                "checkpoint_sha256_changed");
        }

        if (!string.Equals(
                status.LastVerificationManifestSha256,
                status.CheckpointChainLatestManifestSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                status.LastVerificationCheckpointLatestManifestSha256,
                status.CheckpointChainLatestManifestSha256,
                StringComparison.Ordinal))
        {
            return new GatewayActivityVerificationCurrentProof(
                false,
                "manifest_sha256_changed");
        }

        if (!string.Equals(
                status.LastVerificationManifestGeneratedAtUtc,
                status.ManifestGeneratedAtUtc,
                StringComparison.Ordinal))
        {
            return new GatewayActivityVerificationCurrentProof(
                false,
                "manifest_generated_at_changed");
        }

        return new GatewayActivityVerificationCurrentProof(
            true,
            "verification_summary_matches_current_manifest");
    }

    private static bool HasOnlyDiagnosticHandshakeActivitySinceLastVerification(
        GatewayActivityJournalStatus status)
    {
        if (!DateTimeOffset.TryParse(status.LastVerificationAtUtc, out var lastVerificationAt))
        {
            return false;
        }

        var records = GatewayActivityJournal.ReadSince(status.LegacyJournalPath, lastVerificationAt)
            .Where(record => record.Timestamp > lastVerificationAt)
            .ToArray();
        return records.Length > 0 && records.All(IsDiagnosticHandshakeActivity);
    }

    private static bool IsDiagnosticHandshakeActivity(GatewayActivityJournalRecord record)
    {
        return string.Equals(record.Event, "gateway-request", StringComparison.Ordinal)
               && record.Fields.TryGetValue("path", out var path)
               && string.Equals(path, "/handshake", StringComparison.Ordinal);
    }

    private static GatewayActivityMaintenanceFreshnessPosture BuildGatewayActivityMaintenanceFreshnessPosture(
        GatewayActivityJournalStatus status)
    {
        const string mode = "latest_completed_maintenance_summary";
        var thresholdDays = (int)Math.Round(GatewayActivityMaintenanceFreshnessThreshold.TotalDays);

        if (!status.Exists && !status.LegacyJournalExists)
        {
            return new GatewayActivityMaintenanceFreshnessPosture(
                mode,
                thresholdDays,
                "not_applicable",
                -1,
                "start gateway and make a gateway request");
        }

        if (!status.MaintenanceSummaryExists || string.IsNullOrWhiteSpace(status.LastMaintenanceAtUtc))
        {
            return new GatewayActivityMaintenanceFreshnessPosture(
                mode,
                thresholdDays,
                "maintenance_missing",
                -1,
                "gateway activity maintenance");
        }

        if (!DateTimeOffset.TryParse(status.LastMaintenanceAtUtc, out var lastMaintenanceAt))
        {
            return new GatewayActivityMaintenanceFreshnessPosture(
                mode,
                thresholdDays,
                "maintenance_unknown",
                -1,
                "gateway activity maintenance");
        }

        var age = DateTimeOffset.UtcNow - lastMaintenanceAt.ToUniversalTime();
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var ageMinutes = (long)Math.Floor(age.TotalMinutes);
        if (IsFailedMaintenanceSummary(status))
        {
            return new GatewayActivityMaintenanceFreshnessPosture(
                mode,
                thresholdDays,
                "maintenance_failed",
                ageMinutes,
                string.IsNullOrWhiteSpace(status.LastMaintenanceRecommendedAction)
                    ? "gateway activity maintenance"
                    : status.LastMaintenanceRecommendedAction);
        }

        if (age > GatewayActivityMaintenanceFreshnessThreshold)
        {
            return new GatewayActivityMaintenanceFreshnessPosture(
                mode,
                thresholdDays,
                "maintenance_stale",
                ageMinutes,
                "gateway activity maintenance");
        }

        return new GatewayActivityMaintenanceFreshnessPosture(
            mode,
            thresholdDays,
            "maintenance_fresh",
            ageMinutes,
            "no maintenance action needed");
    }

    private static bool IsFailedMaintenanceSummary(GatewayActivityJournalStatus status)
    {
        if (string.Equals(status.LastMaintenanceReason, "segments_archived", StringComparison.Ordinal)
            || string.Equals(status.LastMaintenanceReason, "no_archive_candidates", StringComparison.Ordinal))
        {
            return false;
        }

        return !status.LastMaintenanceApplied
               || status.LastMaintenanceReason.Contains("failed", StringComparison.OrdinalIgnoreCase)
               || status.LastMaintenanceRecommendedAction.StartsWith("inspect ", StringComparison.OrdinalIgnoreCase);
    }

    private static GatewayActivityStatusNextAction BuildGatewayActivityStatusNextAction(
        GatewayActivityStoreOperationalPosture storePosture,
        GatewayActivityVerificationFreshnessPosture verificationFreshness,
        GatewayActivityMaintenanceFreshnessPosture maintenanceFreshness)
    {
        if (storePosture.IssueCount > 0)
        {
            return new GatewayActivityStatusNextAction(
                storePosture.RecommendedAction,
                "store_operational_posture",
                1,
                storePosture.OperationalPosture);
        }

        if (!string.Equals(
                verificationFreshness.RecommendedAction,
                "no verification action needed",
                StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayActivityStatusNextAction(
                verificationFreshness.RecommendedAction,
                "verification_freshness",
                2,
                verificationFreshness.Posture);
        }

        if (!string.Equals(
                maintenanceFreshness.RecommendedAction,
                "no maintenance action needed",
                StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayActivityStatusNextAction(
                maintenanceFreshness.RecommendedAction,
                "maintenance_freshness",
                3,
                maintenanceFreshness.Posture);
        }

        return new GatewayActivityStatusNextAction(
            "no activity store action needed",
            "none",
            0,
            "store_ready_verification_fresh_maintenance_fresh");
    }

    private static string ResolveGatewayActivityStatusExitReason(
        bool operationalOk,
        bool requiresMaturity,
        GatewayActivityStoreOperationalPosture storePosture,
        GatewayActivityMaturityPosture maturity)
    {
        if (!operationalOk)
        {
            return storePosture.OperationalPosture;
        }

        if (requiresMaturity && !maturity.Ready)
        {
            return maturity.Posture;
        }

        return requiresMaturity
            ? "maturity_ready"
            : "operational_ok";
    }

    private static GatewayActivityMaturityPosture BuildGatewayActivityMaturityPosture(
        GatewayActivityStoreOperationalPosture storePosture,
        GatewayActivityVerificationFreshnessPosture verificationFreshness,
        GatewayActivityMaintenanceFreshnessPosture maintenanceFreshness)
    {
        const string stage = "stage_i_bounded_self_maintenance";
        var limitations = new[]
        {
            "operator_diagnosis_only",
            "not_governance_truth",
            "not_compliance_audit_truth",
            "no_default_evidence_deletion",
        };

        if (storePosture.IssueCount > 0)
        {
            var posture = string.Equals(storePosture.OperationalPosture, "store_not_initialized", StringComparison.Ordinal)
                ? "not_initialized"
                : storePosture.OperationalPosture;
            var summary = posture switch
            {
                "not_initialized" => "Stage I self-maintenance cannot be current until Gateway activity initializes the store.",
                "writer_lock_held" => "Stage I self-maintenance is waiting for the current activity writer to release the store lock.",
                "activity_loss_detected" => "Stage I self-maintenance is blocked until recorded activity write drops are inspected.",
                "metadata_incomplete" => "Stage I self-maintenance is blocked until manifest or checkpoint metadata is verified.",
                "legacy_fallback" => "Stage I self-maintenance is limited until the segmented activity store is initialized.",
                _ => "Stage I self-maintenance needs operator attention before it can be considered current.",
            };

            return new GatewayActivityMaturityPosture(
                stage,
                false,
                posture,
                summary,
                limitations);
        }

        if (!string.Equals(verificationFreshness.Posture, "verification_fresh", StringComparison.Ordinal))
        {
            return new GatewayActivityMaturityPosture(
                stage,
                false,
                verificationFreshness.Posture,
                "Stage I self-maintenance still needs a fresh successful activity verification summary.",
                limitations);
        }

        if (!string.Equals(maintenanceFreshness.Posture, "maintenance_fresh", StringComparison.Ordinal))
        {
            return new GatewayActivityMaturityPosture(
                stage,
                false,
                maintenanceFreshness.Posture,
                "Stage I self-maintenance still needs a fresh activity maintenance summary.",
                limitations);
        }

        return new GatewayActivityMaturityPosture(
            stage,
            true,
            "bounded_self_maintenance_current",
            "Stage I self-maintenance is current inside the local operator-diagnosis boundary.",
            limitations);
    }

    private static GatewayActivityCommandNextAction BuildGatewayActivityMaintenanceNextAction(
        GatewayActivityJournalMaintenancePlan maintenance,
        GatewayActivityStoreOperationalPosture storePosture)
    {
        if (maintenance.MaintenanceNeeded)
        {
            return new GatewayActivityCommandNextAction(
                maintenance.RecommendedAction,
                "maintenance_plan",
                2,
                maintenance.Reason);
        }

        if (storePosture.IssueCount > 0)
        {
            return new GatewayActivityCommandNextAction(
                storePosture.RecommendedAction,
                "store_operational_posture",
                1,
                storePosture.OperationalPosture);
        }

        return new GatewayActivityCommandNextAction(
            "no maintenance action needed",
            "none",
            0,
            maintenance.Reason);
    }

    private static GatewayActivityCommandNextAction BuildGatewayActivityExplainNextAction(bool found, string requestId)
    {
        return found
            ? new GatewayActivityCommandNextAction(
                "inspect listed activity entries",
                "request_chain_lookup",
                0,
                "request_chain_found")
            : new GatewayActivityCommandNextAction(
                $"run `gateway activity --request-id {requestId}` or verify the request id",
                "request_chain_lookup",
                2,
                "request_chain_not_found");
    }

    private static GatewayActivityCommandNextAction BuildGatewayActivityVerifyNextAction(
        GatewayActivityJournalVerificationResult verification)
    {
        if (verification.PossiblyTransient)
        {
            return new GatewayActivityCommandNextAction(
                "retry gateway activity verify",
                "verification_snapshot_lock",
                1,
                verification.SnapshotLockStatus);
        }

        return verification.Passed
            ? new GatewayActivityCommandNextAction(
                "gateway activity verified",
                "verification_result",
                0,
                verification.Posture)
            : new GatewayActivityCommandNextAction(
                "inspect gateway activity verify issues",
                "verification_result",
                1,
                verification.Posture);
    }

    private static GatewayActivityCommandNextAction BuildGatewayActivityArchiveNextAction(
        GatewayActivityJournalArchiveResult archive,
        bool succeeded)
    {
        if (archive.Applied)
        {
            return new GatewayActivityCommandNextAction(
                "gateway activity verify",
                "archive_result",
                2,
                archive.Reason);
        }

        return succeeded
            ? new GatewayActivityCommandNextAction(
                "no archive action needed",
                "archive_result",
                0,
                archive.Reason)
            : new GatewayActivityCommandNextAction(
                "inspect gateway activity archive failure",
                "archive_result",
                1,
                archive.Reason);
    }

    private static string FormatActivityTimestamp(GatewayActivityEntry? entry)
    {
        return entry is null ? string.Empty : entry.Timestamp.ToString("O");
    }

    private static string FormatActivitySummary(GatewayActivityEntry? entry)
    {
        if (entry is null)
        {
            return "(none)";
        }

        var command = ResolveActivityField(entry, "command");
        var exitCode = ResolveActivityField(entry, "exit_code");
        var operationId = ResolveActivityField(entry, "operation_id");
        var sessionId = ResolveActivityField(entry, "session_id");
        var messageId = ResolveActivityField(entry, "message_id");
        var requestId = ResolveActivityField(entry, "request_id");
        var requestedAction = ResolveActivityField(entry, "requested_action");
        var method = ResolveActivityField(entry, "method");
        var path = ResolveActivityField(entry, "path");
        var status = ResolveActivityField(entry, "status");
        return $"{entry.Timestamp:O} {entry.Event} request_id={FormatActivityValue(requestId)} command={FormatActivityValue(command)} method={FormatActivityValue(method)} path={FormatActivityValue(path)} session_id={FormatActivityValue(sessionId)} message_id={FormatActivityValue(messageId)} operation_id={FormatActivityValue(operationId)} requested_action={FormatActivityValue(requestedAction)} status={FormatActivityValue(status)} exit_code={FormatActivityValue(exitCode)}";
    }

    private static string FormatGatewayActivityEntryFields(GatewayActivityEntry entry)
    {
        var command = ResolveActivityField(entry, "command");
        var requestId = ResolveActivityField(entry, "request_id");
        var method = ResolveActivityField(entry, "method");
        var path = ResolveActivityField(entry, "path");
        var status = ResolveActivityField(entry, "status");
        var sessionId = ResolveActivityField(entry, "session_id");
        var messageId = ResolveActivityField(entry, "message_id");
        var operationId = ResolveActivityField(entry, "operation_id");
        var requestedAction = ResolveActivityField(entry, "requested_action");
        var operationState = ResolveActivityField(entry, "operation_state");
        var exitCode = ResolveActivityField(entry, "exit_code");
        var arguments = ResolveActivityField(entry, "arguments");
        return string.Join(
            ' ',
            [
                $"request_id={FormatActivityValue(requestId)}",
                $"command={FormatActivityValue(command)}",
                $"method={FormatActivityValue(method)}",
                $"path={FormatActivityValue(path)}",
                $"status={FormatActivityValue(status)}",
                $"session_id={FormatActivityValue(sessionId)}",
                $"message_id={FormatActivityValue(messageId)}",
                $"operation_id={FormatActivityValue(operationId)}",
                $"requested_action={FormatActivityValue(requestedAction)}",
                $"operation_state={FormatActivityValue(operationState)}",
                $"exit_code={FormatActivityValue(exitCode)}",
                $"arguments={FormatActivityValue(arguments)}",
            ]);
    }

    private static string ResolveActivityField(GatewayActivityEntry? entry, string key)
    {
        return entry is not null && entry.Fields.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string ResolveFirstActivityField(IReadOnlyList<GatewayActivityEntry> entries, string key)
    {
        foreach (var entry in entries)
        {
            var value = ResolveActivityField(entry, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ResolveLastActivityField(IReadOnlyList<GatewayActivityEntry> entries, string key)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var value = ResolveActivityField(entries[index], key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ResolveGatewayActivityExplainRequestId(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 2 && !arguments[2].StartsWith("--", StringComparison.Ordinal))
        {
            return arguments[2].Trim();
        }

        return ResolveOption(arguments, "--request-id")?.Trim() ?? string.Empty;
    }

    private static string FormatActivityValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }

    private static string FormatActivityIssues(IReadOnlyList<string> issues)
    {
        return issues.Count == 0 ? "none" : string.Join(", ", issues);
    }

    private static string FormatLogPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "(none)" : path;
    }

    private static string ToGatewayAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "carves gateway status --json";
        }

        return action
            .Replace("carves host", "carves gateway", StringComparison.OrdinalIgnoreCase)
            .Replace("host ready", "gateway ready", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> PrefixTail(IReadOnlyList<string> lines)
    {
        return lines.Count == 0 ? ["  (empty)"] : lines.Select(static line => $"  {line}").ToArray();
    }

    private static IReadOnlyList<string> ReadLogTail(string path, int lineCount)
    {
        if (lineCount <= 0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            var tail = new Queue<string>(capacity: lineCount);
            foreach (var line in File.ReadLines(path))
            {
                if (tail.Count == lineCount)
                {
                    tail.Dequeue();
                }

                tail.Enqueue(line);
            }

            return tail.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [$"(unable to read log: {exception.Message})"];
        }
    }

    private sealed record GatewayLogProjection(
        string DeploymentDirectory,
        string StandardOutputLogPath,
        string StandardErrorLogPath,
        bool StandardOutputLogExists,
        bool StandardErrorLogExists,
        bool LogsAvailable,
        int TailLineCount,
        IReadOnlyList<string> StandardOutputTail,
        IReadOnlyList<string> StandardErrorTail);

    private sealed record GatewayActivityEntry(
        DateTimeOffset Timestamp,
        string Event,
        IReadOnlyDictionary<string, string> Fields);

    private sealed record GatewayActivityStoreOperationalPosture(
        string OperationalPosture,
        string OperatorSummary,
        int IssueCount,
        IReadOnlyList<string> Issues,
        string RecommendedAction);

    private sealed record GatewayActivityStatusNextAction(
        string Action,
        string Source,
        int Priority,
        string Reason);

    private sealed record GatewayActivityDiagnosticNextAction(
        string Action,
        string Source,
        int Priority,
        string Reason);

    private sealed record GatewayActivityCommandNextAction(
        string Action,
        string Source,
        int Priority,
        string Reason);

    private sealed record GatewayActivityMaturityPosture(
        string Stage,
        bool Ready,
        string Posture,
        string Summary,
        IReadOnlyList<string> Limitations);

    private sealed record GatewayActivityVerificationFreshnessPosture(
        string Mode,
        int ThresholdHours,
        string Posture,
        long AgeMinutes,
        string RecommendedAction,
        bool CurrentProof,
        string CurrentProofReason);

    private sealed record GatewayActivityVerificationCurrentProof(
        bool CurrentProof,
        string Reason);

    private sealed record GatewayActivityMaintenanceFreshnessPosture(
        string Mode,
        int ThresholdDays,
        string Posture,
        long AgeMinutes,
        string RecommendedAction);

    private sealed record GatewayActivityProjection(
        bool ActivityAvailable,
        int EntryCount,
        GatewayActivityEntry? LastEntry,
        GatewayActivityEntry? LastRequest,
        GatewayActivityEntry? LastFeedback,
        GatewayActivityEntry? LastCliInvoke,
        GatewayActivityEntry? LastRestRequest,
        GatewayActivityEntry? LastSessionMessage,
        GatewayActivityEntry? LastOperationFeedback,
        GatewayActivityEntry? LastRouteNotFound,
        GatewayActivityEntry? LastGatewayRequest,
        string ActivitySource,
        string ActivityQueryMode,
        bool ActivityQueryStoreBackedSince,
        bool ActivityQueryTailBounded,
        string ActivityJournalPath,
        string ActivityJournalStorageMode,
        string ActivityJournalStoreDirectory,
        string ActivityJournalSegmentDirectory,
        string ActivityJournalArchiveDirectory,
        string ActivityStoreOperationalPosture,
        string ActivityStoreOperatorSummary,
        int ActivityStoreIssueCount,
        IReadOnlyList<string> ActivityStoreIssues,
        string ActivityStoreRecommendedAction,
        string ActivityStoreVerificationFreshnessMode,
        int ActivityStoreVerificationFreshnessThresholdHours,
        string ActivityStoreVerificationFreshnessPosture,
        long ActivityStoreVerificationFreshnessAgeMinutes,
        string ActivityStoreVerificationFreshnessRecommendedAction,
        bool ActivityStoreVerificationCurrentProof,
        string ActivityStoreVerificationCurrentProofReason,
        string ActivityStoreMaintenanceFreshnessMode,
        int ActivityStoreMaintenanceFreshnessThresholdDays,
        string ActivityStoreMaintenanceFreshnessPosture,
        long ActivityStoreMaintenanceFreshnessAgeMinutes,
        string ActivityStoreMaintenanceFreshnessRecommendedAction,
        string ActivityStoreMaturityStage,
        bool ActivityStoreMaturityReady,
        string ActivityStoreMaturityPosture,
        string ActivityStoreMaturitySummary,
        IReadOnlyList<string> ActivityStoreMaturityLimitations,
        string ActivityStoreRetentionMode,
        string ActivityStoreRetentionExecutionMode,
        int ActivityStoreDefaultArchiveBeforeDays,
        string ActivityStoreWriterLockMode,
        string ActivityStoreWriterLockPath,
        bool ActivityStoreWriterLockExists,
        bool ActivityStoreWriterLockFileExists,
        bool ActivityStoreWriterLockCurrentlyHeld,
        string ActivityStoreWriterLockStatus,
        string ActivityStoreWriterLockLastHolderProcessId,
        string ActivityStoreWriterLockLastAcquiredAtUtc,
        int ActivityStoreWriterLockAcquireTimeoutMs,
        string ActivityStoreIntegrityMode,
        string ActivityStoreDropTelemetryPath,
        bool ActivityStoreDropTelemetryExists,
        long ActivityStoreDroppedActivityCount,
        string ActivityStoreLastDropAtUtc,
        string ActivityStoreLastDropReason,
        string ActivityStoreLastDropEvent,
        string ActivityStoreLastDropRequestId,
        int ActivityJournalArchiveSegmentCount,
        long ActivityJournalArchiveByteCount,
        string ActivityJournalActiveSegmentPath,
        int ActivityJournalSegmentCount,
        string ActivityJournalManifestPath,
        bool ActivityJournalManifestExists,
        string ActivityJournalManifestSchemaVersion,
        string ActivityJournalCheckpointChainPath,
        bool ActivityJournalCheckpointChainExists,
        int ActivityJournalCheckpointChainCount,
        long ActivityJournalCheckpointChainLatestSequence,
        string ActivityJournalCheckpointChainLatestCheckpointSha256,
        string ActivityJournalCheckpointChainLatestManifestSha256,
        string ActivityJournalManifestGeneratedAtUtc,
        long ActivityJournalManifestRecordCount,
        long ActivityJournalManifestByteCount,
        string ActivityJournalManifestFirstTimestampUtc,
        string ActivityJournalManifestLastTimestampUtc,
        IReadOnlyList<GatewayActivityJournalSegmentManifest> ActivityJournalSegments,
        string ActivityJournalLegacyPath,
        bool ActivityJournalLegacyExists,
        bool ActivityJournalLegacyFallbackUsed,
        bool ActivityJournalExists,
        long ActivityJournalByteCount,
        long ActivityJournalLineCount,
        IReadOnlyList<GatewayActivityEntry> Entries);

    private sealed record GatewayActivityFilter(
        bool Valid,
        string Category,
        string EventKind,
        string Command,
        string Path,
        string RequestId,
        string OperationId,
        string SessionId,
        string MessageId,
        int SinceMinutes,
        DateTimeOffset? SinceUtc,
        string Error)
    {
        public bool Applied => !string.Equals(Category, GatewayActivityEventKinds.CategoryAll, StringComparison.Ordinal)
                               || !string.IsNullOrWhiteSpace(EventKind)
                               || !string.IsNullOrWhiteSpace(Command)
                               || !string.IsNullOrWhiteSpace(Path)
                               || !string.IsNullOrWhiteSpace(RequestId)
                               || !string.IsNullOrWhiteSpace(OperationId)
                               || !string.IsNullOrWhiteSpace(SessionId)
                               || !string.IsNullOrWhiteSpace(MessageId)
                               || SinceMinutes > 0;

        public static GatewayActivityFilter Invalid(string error)
        {
            return new GatewayActivityFilter(
                false,
                GatewayActivityEventKinds.CategoryAll,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                null,
                error);
        }
    }

    private static OperatorCommandResult RenderHostStatus(string repoRoot, HostDiscoveryResult discovery, string? requiredCapability)
    {
        var honesty = LocalHostSurfaceHonesty.Describe(discovery);
        var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        var statusReady = discovery.HostRunning && commandSurfaceCompatibility.Compatible;
        var payload = new
        {
            SchemaVersion = "carves-host-status.v1",
            RepoRoot = repoRoot,
            HostRunning = discovery.HostRunning,
            HostReadiness = honesty.HostReadiness,
            HostOperationalState = honesty.OperationalState,
            GatewayRole = "connection_routing_observability",
            GatewayAutomationBoundary = "no_worker_automation_dispatch",
            ConflictPresent = honesty.ConflictPresent,
            SafeToStartNewHost = honesty.SafeToStartNewHost,
            PointerRepairApplied = honesty.PointerRepairApplied,
            RequiredCapability = requiredCapability ?? string.Empty,
            RecommendedActionKind = honesty.RecommendedActionKind,
            RecommendedAction = honesty.RecommendedAction,
            Lifecycle = honesty.Lifecycle,
            Message = honesty.SummaryMessage,
            BaseUrl = discovery.Summary?.BaseUrl ?? honesty.BaseUrl ?? string.Empty,
            DashboardUrl = discovery.Summary?.DashboardUrl ?? string.Empty,
            RuntimeDirectory = discovery.Summary?.RuntimeDirectory ?? discovery.Descriptor?.RuntimeDirectory ?? discovery.Snapshot?.RuntimeDirectory ?? string.Empty,
            DeploymentDirectory = discovery.Summary?.DeploymentDirectory ?? discovery.Descriptor?.DeploymentDirectory ?? discovery.Snapshot?.DeploymentDirectory ?? string.Empty,
            ProcessId = honesty.ProcessId,
            SnapshotState = honesty.SnapshotState,
            SnapshotRecordedAt = discovery.Snapshot?.RecordedAt,
            SnapshotSummary = discovery.Snapshot?.Summary ?? string.Empty,
            Capabilities = discovery.Summary?.Capabilities ?? Array.Empty<string>(),
            HostCommandSurfaceCompatible = commandSurfaceCompatibility.Compatible,
            HostCommandSurfaceReadiness = commandSurfaceCompatibility.Readiness,
            HostCommandSurfaceReason = commandSurfaceCompatibility.Reason,
            HostCommandSurfaceExpectedSchemaVersion = commandSurfaceCompatibility.ExpectedSchemaVersion,
            HostCommandSurfaceActualSchemaVersion = commandSurfaceCompatibility.ActualSchemaVersion ?? string.Empty,
            ExpectedCommandSurfaceFingerprint = commandSurfaceCompatibility.ExpectedFingerprint,
            ActualCommandSurfaceFingerprint = commandSurfaceCompatibility.ActualFingerprint ?? string.Empty,
            ExpectedCommandSurfaceCommandCount = commandSurfaceCompatibility.ExpectedCommandCount,
            ActualCommandSurfaceCommandCount = commandSurfaceCompatibility.ActualCommandCount,
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        });
        return new OperatorCommandResult(statusReady ? 0 : 1, [json]);
    }

    private static OperatorCommandResult StopHost(CommandLineArguments commandLine)
    {
        var force = commandLine.Arguments.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));
        var reasonParts = commandLine.Arguments
            .Skip(1)
            .Where(argument => !string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var reason = reasonParts.Length == 0
            ? "Host stop requested by operator."
            : string.Join(' ', reasonParts);
        return new LocalHostClient(commandLine.RepoRoot).Stop(reason, force);
    }

    private static OperatorCommandResult PauseHost(CommandLineArguments commandLine)
    {
        var reasonParts = commandLine.Arguments.Skip(1).ToArray();
        if (reasonParts.Length == 0)
        {
            return OperatorCommandResult.Failure("Usage: host pause <reason...>");
        }

        return new LocalHostClient(commandLine.RepoRoot).Control("pause", string.Join(' ', reasonParts));
    }

    private static OperatorCommandResult ResumeHost(CommandLineArguments commandLine)
    {
        var reasonParts = commandLine.Arguments.Skip(1).ToArray();
        var reason = reasonParts.Length == 0
            ? "Host resumed by operator."
            : string.Join(' ', reasonParts);
        return new LocalHostClient(commandLine.RepoRoot).Control("resume", reason);
    }

    private static OperatorCommandResult FormatHostDiscovery(string repoRoot, HostDiscoveryResult discovery, string? preface = null)
    {
        var honesty = LocalHostSurfaceHonesty.Describe(discovery);
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            var missingLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(preface))
            {
                missingLines.Add(preface);
            }

            missingLines.Add(discovery.Message);
            if (discovery.Snapshot is not null)
            {
                missingLines.Add($"Snapshot state: {discovery.Snapshot.State}");
                missingLines.Add($"Snapshot recorded at: {discovery.Snapshot.RecordedAt:O}");
                missingLines.Add($"Snapshot summary: {discovery.Snapshot.Summary}");
            }

            missingLines.Add($"Host readiness: {honesty.HostReadiness}");
            missingLines.Add($"Host operational state: {honesty.OperationalState}");
            missingLines.Add("Gateway role: connection, routing, and observability only");
            missingLines.Add("Gateway automation boundary: worker automation is controlled separately by role-mode gates");
            missingLines.Add($"Conflict present: {honesty.ConflictPresent}");
            missingLines.Add($"Safe to start new host: {honesty.SafeToStartNewHost}");
            missingLines.Add($"Recommended action: {honesty.RecommendedAction}");
            missingLines.Add(honesty.SafeToStartNewHost
                ? $"Run `{HostEnsureFirstCommand}` before relying on resident-host execution."
                : $"Use `{honesty.RecommendedAction}` before relying on resident-host execution.");
            return new OperatorCommandResult(1, missingLines);
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(preface))
        {
            lines.Add(preface);
        }

        var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        lines.Add("CARVES Host summary");
        lines.Add($"Repo root: {repoRoot}");
        lines.Add($"Base URL: {discovery.Summary.BaseUrl}");
        lines.Add($"Dashboard: {discovery.Summary.DashboardUrl}");
        lines.Add($"Runtime stage: {discovery.Summary.Stage}");
        lines.Add($"Host session id: {discovery.Summary.HostSessionId}");
        lines.Add($"CARVES standard: v{discovery.Summary.StandardVersion}");
        lines.Add($"Uptime: {discovery.Summary.UptimeSeconds}s");
        lines.Add($"Host runtime dir: {discovery.Summary.RuntimeDirectory}");
        lines.Add($"Host deployment dir: {discovery.Summary.DeploymentDirectory ?? "(unknown)"}");
        if (!string.IsNullOrWhiteSpace(discovery.Summary.DeploymentDirectory))
        {
            lines.Add($"Host stdout log: {LocalHostProcessLauncher.GetStandardOutputLogPath(discovery.Summary.DeploymentDirectory)}");
            lines.Add($"Host stderr log: {LocalHostProcessLauncher.GetStandardErrorLogPath(discovery.Summary.DeploymentDirectory)}");
        }

        if (discovery.Snapshot is not null)
        {
            lines.Add($"Host snapshot state: {discovery.Snapshot.State}");
            lines.Add($"Host snapshot recorded at: {discovery.Snapshot.RecordedAt:O}");
            lines.Add($"Host snapshot summary: {discovery.Snapshot.Summary}");
        }

        lines.Add($"Repos: {discovery.Summary.RepoCount}");
        lines.Add($"Attached repos: {discovery.Summary.AttachedRepoCount}");
        lines.Add($"Active sessions: {discovery.Summary.ActiveSessionCount}");
        lines.Add($"Planner state: {discovery.Summary.PlannerState}");
        lines.Add($"Workers: {discovery.Summary.WorkerCount} (active {discovery.Summary.ActiveWorkerCount})");
        lines.Add($"Actor sessions: {discovery.Summary.ActorSessionCount}");
        lines.Add($"Operator OS events: {discovery.Summary.OperatorOsEventCount}");
        lines.Add($"Rehydrated: {discovery.Summary.Rehydrated}");
        lines.Add($"Pending approvals: {discovery.Summary.PendingApprovalCount}");
        lines.Add($"Recent incidents: {discovery.Summary.RecentIncidentCount}");
        lines.Add($"Stale markers cleaned: {discovery.Summary.StaleMarkerCount}");
        lines.Add($"Runtime instances paused on restart: {discovery.Summary.PausedRuntimeCount}");
        lines.Add($"Rehydration summary: {discovery.Summary.RehydrationSummary}");
        lines.Add($"Host control state: {discovery.Summary.HostControlState}");
        lines.Add($"Host control action: {discovery.Summary.HostControlAction}");
        lines.Add($"Host control reason: {discovery.Summary.HostControlReason ?? "(none)"}");
        lines.Add($"Protocol mode: {discovery.Summary.ProtocolMode}");
        lines.Add($"Conversation phase: {discovery.Summary.ConversationPhase}");
        lines.Add($"Intent state: {discovery.Summary.IntentState}");
        lines.Add($"Prompt kernel: {discovery.Summary.PromptKernel}");
        lines.Add($"Project understanding: {discovery.Summary.ProjectUnderstandingState}");
        lines.Add($"Host readiness: {honesty.HostReadiness}");
        lines.Add($"Host operational state: {honesty.OperationalState}");
        lines.Add("Gateway role: connection, routing, and observability only");
        lines.Add("Gateway automation boundary: worker automation is controlled separately by role-mode gates");
        lines.Add($"Conflict present: {honesty.ConflictPresent}");
        lines.Add($"Safe to start new host: {honesty.SafeToStartNewHost}");
        lines.Add($"Pointer repair applied: {honesty.PointerRepairApplied}");
        lines.Add($"Recommended action kind: {honesty.RecommendedActionKind}");
        lines.Add($"Recommended action: {honesty.RecommendedAction}");
        lines.Add($"Lifecycle state: {honesty.Lifecycle.State}");
        lines.Add($"Lifecycle reason: {honesty.Lifecycle.Reason}");
        lines.Add($"Host command surface: {commandSurfaceCompatibility.Readiness}");
        lines.Add($"Host command surface reason: {commandSurfaceCompatibility.Reason}");
        lines.Add($"Host command surface expected fingerprint: {commandSurfaceCompatibility.ExpectedFingerprint}");
        lines.Add($"Host command surface actual fingerprint: {commandSurfaceCompatibility.ActualFingerprint ?? "(missing)"}");
        lines.Add($"Capabilities: {string.Join(", ", discovery.Summary.Capabilities)}");
        return new OperatorCommandResult(commandSurfaceCompatibility.Compatible ? 0 : 1, lines);
    }

    private static string? ResolveHostCapability(string command, IReadOnlyList<string> arguments)
    {
        return HostCommandRoutingCatalog.ResolveCapability(command, arguments);
    }

    private static string? ResolveMinimumStatefulActionFamily(string command, IReadOnlyList<string> arguments)
    {
        return HostCommandRoutingCatalog.ResolveMinimumStatefulActionFamily(command, arguments);
    }

    private static string? ResolveRequiredCapabilityForMinimumStatefulActionFamily(string? family)
    {
        return HostCommandRoutingCatalog.ResolveRequiredCapabilityForMinimumStatefulActionFamily(family);
    }

    private static bool IsControlPlaneMutationCommand(string command, IReadOnlyList<string> arguments)
    {
        return ResolveHostCapability(command, arguments) == "control-plane-mutation";
    }

    private static bool ShouldEmitHostFallbackNotice(string command)
    {
        return HostCommandRoutingCatalog.ShouldEmitFallbackNotice(command);
    }

    private static string BuildHostFallbackNotice(string command, HostDiscoveryResult discovery)
    {
        var honesty = LocalHostSurfaceHonesty.Describe(discovery);
        if (honesty.ConflictPresent)
        {
            return $"Resident host session conflict is present; executing `{command}` through the cold path. Use `{honesty.RecommendedAction}` before relying on resident-host execution.";
        }

        return $"No resident host is running; executing `{command}` through the cold path.";
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
