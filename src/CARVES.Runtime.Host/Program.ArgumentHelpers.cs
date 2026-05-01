using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static OperatorCommandResult RunAgentCommand(CommandLineArguments commandLine, RuntimeServices? services)
    {
        if (commandLine.Arguments.Count > 0
            && (string.Equals(commandLine.Arguments[0], "start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandLine.Arguments[0], "boot", StringComparison.OrdinalIgnoreCase)))
        {
            if (services is null)
            {
                services = RuntimeComposition.Create(commandLine.RepoRoot);
            }

            return commandLine.Arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentThreadStart()
                : services.OperatorSurfaceService.InspectRuntimeAgentThreadStart();
        }

        if (commandLine.Arguments.Count > 0
            && (string.Equals(commandLine.Arguments[0], "context", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandLine.Arguments[0], "short-context", StringComparison.OrdinalIgnoreCase)))
        {
            if (services is null)
            {
                services = RuntimeComposition.Create(commandLine.RepoRoot);
            }

            var contextArguments = commandLine.Arguments.Skip(1).ToArray();
            var taskId = ResolvePrimaryArgument(contextArguments, Array.Empty<string>(), ["--json", "api"]);
            return commandLine.Arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentShortContext(taskId)
                : services.OperatorSurfaceService.InspectRuntimeAgentShortContext(taskId);
        }

        if (commandLine.Arguments.Count > 0
            && string.Equals(commandLine.Arguments[0], "handoff", StringComparison.OrdinalIgnoreCase))
        {
            if (services is null)
            {
                services = RuntimeComposition.Create(commandLine.RepoRoot);
            }

            return commandLine.Arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeGovernedAgentHandoffProof()
                : services.OperatorSurfaceService.InspectRuntimeGovernedAgentHandoffProof();
        }

        if (commandLine.Arguments.Count > 0
            && string.Equals(commandLine.Arguments[0], "bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            if (services is null)
            {
                services = RuntimeComposition.Create(commandLine.RepoRoot);
            }

            var writeRequested = commandLine.Arguments.Any(argument => string.Equals(argument, "--write", StringComparison.OrdinalIgnoreCase)
                                                                       || string.Equals(argument, "--materialize", StringComparison.OrdinalIgnoreCase));
            return commandLine.Arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetAgentBootstrapPack(writeRequested)
                : services.OperatorSurfaceService.InspectRuntimeTargetAgentBootstrapPack(writeRequested);
        }

        if (commandLine.Arguments.Count > 0
            && (string.Equals(commandLine.Arguments[0], "trace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandLine.Arguments[0], "gateway-trace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandLine.Arguments[0], "gateway_trace", StringComparison.OrdinalIgnoreCase)))
        {
            if (services is null)
            {
                services = RuntimeComposition.Create(commandLine.RepoRoot);
            }

            var jsonRequested = commandLine.Arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "api", StringComparison.OrdinalIgnoreCase));
            var watchRequested = commandLine.Arguments.Any(argument => string.Equals(argument, "--watch", StringComparison.OrdinalIgnoreCase));
            if (watchRequested && jsonRequested)
            {
                return OperatorCommandResult.Failure("Usage: agent trace --watch renders text snapshots; use agent trace --json without --watch for a single JSON payload.");
            }

            if (watchRequested)
            {
                return RunAgentTraceWatch(services, commandLine.Arguments);
            }

            if (jsonRequested)
            {
                var surface = new LocalHostSurfaceService(services);
                return OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildAgentGatewayTrace()));
            }

            return services.OperatorSurfaceService.AgentGatewayTrace();
        }

        if (commandLine.Arguments.Count < 2)
        {
            return OperatorCommandResult.Failure("Usage: agent <start [--json]|context [<task-id>] [--json]|handoff [--json]|bootstrap [--write] [--json]|trace|query|request|report> <operation> [target-id]");
        }

        var operationClass = commandLine.Arguments[0];
        var operation = commandLine.Arguments[1];
        var targetId = commandLine.Arguments.Count >= 3 ? commandLine.Arguments[2] : null;
        var envelope = new AgentRequestEnvelope(
            Guid.NewGuid().ToString("N"),
            operationClass,
            operation,
            targetId,
            ActorIdentity: "agent-cli",
            ActorSessionId: null,
            Arguments: new JsonObject(),
            Payload: null);

        if (services is not null)
        {
            var response = new LocalHostSurfaceService(services).HandleAgent(envelope);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }));
        }

        var client = new LocalHostClient(commandLine.RepoRoot);
        var discovery = client.Discover("agent-gateway");
        if (!discovery.HostRunning)
        {
            return OperatorCommandResult.Failure(discovery.Message, "Run `carves host ensure --json` before calling the agent gateway.");
        }

        var result = client.SendAgent(envelope);
        return result.Accepted
            ? OperatorCommandResult.Success(JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }))
            : OperatorCommandResult.Failure(JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }));
    }

    private static OperatorCommandResult RunAgentTraceWatch(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var iterations = ResolveOptionalPositiveInt(arguments, "--iterations", defaultValue: 3);
        var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", defaultValue: 1500, allowZero: true);
        var lines = new List<string>();
        for (var cycle = 0; cycle < iterations; cycle++)
        {
            if (cycle > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"=== CARVES Agent Gateway Trace Watch {cycle + 1}/{iterations} {DateTimeOffset.Now:O} ===");
            lines.AddRange(services.OperatorSurfaceService.AgentGatewayTrace().Lines);
            if (cycle + 1 >= iterations)
            {
                break;
            }

            if (intervalMilliseconds > 0)
            {
                Thread.Sleep(intervalMilliseconds);
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    private static int ResolveIterationCount(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], "--iterations", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || parsed <= 0)
            {
                throw new InvalidOperationException("Usage: session loop [--dry-run] [--iterations <positive-integer>]");
            }

            return parsed;
        }

        return 5;
    }

    private static string? ResolveOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a value.");
            }

            return arguments[index + 1];
        }

        return null;
    }

    private static string? ResolveValidateSafetyOperation(IReadOnlyList<string> arguments)
    {
        var optionValue = ResolveOption(arguments, "--op");
        if (!string.IsNullOrWhiteSpace(optionValue))
        {
            return optionValue;
        }

        if (arguments.Any(argument => string.Equals(argument, "--read", StringComparison.OrdinalIgnoreCase)))
        {
            return "read";
        }

        if (arguments.Any(argument => string.Equals(argument, "--write", StringComparison.OrdinalIgnoreCase)))
        {
            return "write";
        }

        if (arguments.Any(argument => string.Equals(argument, "--delete", StringComparison.OrdinalIgnoreCase)))
        {
            return "delete";
        }

        if (arguments.Any(argument => string.Equals(argument, "--execute", StringComparison.OrdinalIgnoreCase)))
        {
            return "execute";
        }

        return null;
    }

    private static string ResolveWorkbenchUrl(string baseUrl, IReadOnlyList<string> arguments)
    {
        var filtered = arguments.Where(argument => !string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (filtered.Length == 0 || string.Equals(filtered[0], "overview", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl.TrimEnd('/')}/workbench";
        }

        if (filtered.Length >= 2 && string.Equals(filtered[0], "card", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl.TrimEnd('/')}/workbench/card/{Uri.EscapeDataString(filtered[1])}";
        }

        if (filtered.Length >= 2 && string.Equals(filtered[0], "task", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl.TrimEnd('/')}/workbench/task/{Uri.EscapeDataString(filtered[1])}";
        }

        if (string.Equals(filtered[0], "review", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl.TrimEnd('/')}/workbench/review";
        }

        return $"{baseUrl.TrimEnd('/')}/workbench";
    }

    private static IReadOnlyList<string> ResolveValidateSafetyPaths(IReadOnlyList<string> arguments)
    {
        var paths = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--actor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--op", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (string.Equals(argument, "--path", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("Usage error: option '--path' requires a value.");
                }

                paths.Add(arguments[index + 1]);
                index++;
                continue;
            }

            if (string.Equals(argument, "--read", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--write", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--delete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--execute", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            paths.Add(argument);
        }

        return paths;
    }

    private static int ResolveSlots(IReadOnlyList<string> arguments)
    {
        return ResolveOptionalPositiveInt(arguments, "--slots", 1);
    }

    private static int ResolveOptionalPositiveInt(IReadOnlyList<string> arguments, string option, int defaultValue, bool allowZero = false)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || (allowZero ? parsed < 0 : parsed <= 0))
            {
                throw new InvalidOperationException($"Usage: {option} <{(allowZero ? "non-negative" : "positive")}-integer>");
            }

            return parsed;
        }

        return defaultValue;
    }

    private static int? ResolveOptionalInt(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || parsed <= 0)
            {
                throw new InvalidOperationException($"Usage: {option} <positive-integer>");
            }

            return parsed;
        }

        return null;
    }

    private static PlannerWakeReason ParsePlannerWakeReason(string? value, PlannerWakeReason fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<PlannerWakeReason>(normalized, true, out var parsed) ? parsed : fallback;
    }

    private static PlannerSleepReason ParsePlannerSleepReason(string? value, PlannerSleepReason fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<PlannerSleepReason>(normalized, true, out var parsed) ? parsed : fallback;
    }

    private static string? ResolvePrimaryArgument(IReadOnlyList<string> arguments, IReadOnlyCollection<string> optionsWithValues, IReadOnlyCollection<string> flagOptions)
    {
        var consumedIndexes = new HashSet<int>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (optionsWithValues.Contains(arguments[index], StringComparer.OrdinalIgnoreCase))
            {
                consumedIndexes.Add(index);
                if (index + 1 < arguments.Count)
                {
                    consumedIndexes.Add(index + 1);
                }

                continue;
            }

            if (flagOptions.Contains(arguments[index], StringComparer.OrdinalIgnoreCase))
            {
                consumedIndexes.Add(index);
            }
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (!consumedIndexes.Contains(index))
            {
                return arguments[index];
            }
        }

        return null;
    }

    private static ActorSessionKind? ParseActorSessionKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ActorSessionKind>(value, true, out var parsed) ? parsed : null;
    }

    private static OwnershipScope? ParseOwnershipScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<OwnershipScope>(value, true, out var parsed) ? parsed : null;
    }

    private static OperatorOsEventKind? ParseOperatorOsEventKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<OperatorOsEventKind>(value, true, out var parsed) ? parsed : null;
    }

    private static RoutingValidationMode ParseRoutingValidationMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "baseline" => RoutingValidationMode.Baseline,
            "forced-fallback" or "forced_fallback" => RoutingValidationMode.ForcedFallback,
            _ => RoutingValidationMode.Routing,
        };
    }

    private sealed record CommandLineArguments(string RepoRoot, string Command, IReadOnlyList<string> Arguments)
    {
        public bool UseColdPath { get; init; }

        public static CommandLineArguments Parse(IReadOnlyList<string> args)
        {
            var repoRoot = Directory.GetCurrentDirectory();
            var remaining = new List<string>();
            var useColdPath = false;

            for (var index = 0; index < args.Count; index++)
            {
                if (string.Equals(args[index], "--repo-root", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
                {
                    repoRoot = Path.GetFullPath(args[index + 1]);
                    index++;
                    continue;
                }

                if (string.Equals(args[index], "--cold", StringComparison.OrdinalIgnoreCase))
                {
                    useColdPath = true;
                    continue;
                }

                remaining.Add(args[index]);
            }

            var command = remaining.Count == 0 ? "discover" : remaining[0];
            return new CommandLineArguments(repoRoot, command, remaining.Skip(1).ToArray())
            {
                UseColdPath = useColdPath,
            };
        }
    }
}
