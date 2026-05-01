using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private const string RuntimeRootEnvironmentVariable = "CARVES_RUNTIME_ROOT";

    private static readonly ManualResetEventSlim CliWatchDelayGate = new(initialState: false);

    private static int Delegate(
        string repoRoot,
        string command,
        IReadOnlyList<string>? arguments = null,
        TransportPreference transport = TransportPreference.Auto)
    {
        if (transport == TransportPreference.Host)
        {
            return EnsureHostAndDelegate(repoRoot, command, arguments ?? Array.Empty<string>());
        }

        var invocation = new List<string>();
        if (transport == TransportPreference.Cold)
        {
            invocation.Add("--cold");
        }

        invocation.Add(command);
        invocation.AddRange(arguments ?? Array.Empty<string>());

        var result = HostProgramInvoker.Invoke(repoRoot, invocation.ToArray());
        result.WriteToConsole();
        return result.ExitCode;
    }

    private static int EnsureHostAndDelegate(string repoRoot, string command, IReadOnlyList<string> arguments)
    {
        var hostProjection = ResolveFriendlyHostProjection(repoRoot);
        if (!hostProjection.HostRunning)
        {
            return RenderFriendlyHostTransportFailure(hostProjection, exitCode: 3);
        }

        var result = HostProgramInvoker.Invoke(repoRoot, [command, .. arguments]);
        result.WriteToConsole();
        return result.ExitCode;
    }

    private static int RunWorkbenchWatch(
        string repoRoot,
        IReadOnlyList<string> arguments,
        TransportPreference transport,
        int intervalMilliseconds,
        int iterations)
    {
        if (transport == TransportPreference.Host)
        {
            var hostProjection = ResolveFriendlyHostProjection(repoRoot);
            if (!hostProjection.HostRunning)
            {
                return RenderFriendlyHostTransportFailure(hostProjection, exitCode: 3);
            }
        }

        var invocation = new List<string>();
        if (transport == TransportPreference.Cold)
        {
            invocation.Add("--cold");
        }

        invocation.Add("workbench");
        if (!arguments.Any(argument => string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase)))
        {
            invocation.Add("--text");
        }

        invocation.AddRange(arguments);

        var cycle = 0;
        while (iterations == 0 || cycle < iterations)
        {
            var result = HostProgramInvoker.Invoke(repoRoot, invocation.ToArray());
            if (cycle > 0)
            {
                Console.WriteLine();
            }

            Console.WriteLine($"=== CARVES Workbench {DateTimeOffset.Now:O} ===");
            result.WriteToConsole();
            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }

            cycle += 1;
            if (iterations > 0 && cycle >= iterations)
            {
                break;
            }

            if (intervalMilliseconds > 0)
            {
                DelayWorkbenchWatch(intervalMilliseconds);
            }
        }

        return 0;
    }

    private static void DelayWorkbenchWatch(int intervalMilliseconds)
    {
        DelayCliWatch(intervalMilliseconds);
    }

    private static void DelayCliWatch(int intervalMilliseconds)
    {
        CliWatchDelayGate.Wait(TimeSpan.FromMilliseconds(intervalMilliseconds));
    }

    private static IReadOnlyList<string> FilterWorkbenchArguments(IReadOnlyList<string> arguments)
    {
        var filtered = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--watch", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(argument, "--interval-ms", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--iterations", StringComparison.OrdinalIgnoreCase))
            {
                index += 1;
                continue;
            }

            filtered.Add(argument);
        }

        return filtered;
    }

    private static void RenderFriendlySummary(string repoRoot, string runtimeMode, FriendlyHostProjection hostProjection)
    {
        var services = RuntimeComposition.Create(repoRoot);
        var manifest = new RuntimeManifestService(services.Paths).Load();
        var health = new RuntimeHealthCheckService(services.Paths, services.TaskGraphService).Evaluate();
        var graph = services.TaskGraphService.Load();
        var session = services.DevLoopService.GetSession();
        var dispatch = services.DispatchProjectionService.Build(graph, session, services.SystemConfig.MaxParallelTasks);
        var projectName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var workerStatus = session is not null && session.ActiveWorkerCount > 0
            ? $"busy ({session.ActiveWorkerCount} active)"
            : "idle";
        var nextAction = ResolveNextAction(hostProjection, health.State, dispatch);

        Console.WriteLine($"Project: {projectName}");
        Console.WriteLine($"Host: {DescribeFriendlyHostLabel(hostProjection)}");
        Console.WriteLine($"Host operational state: {hostProjection.HostOperationalState}");
        if (!hostProjection.HostRunning)
        {
            Console.WriteLine($"Host recommended action: {ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json")}");
        }
        Console.WriteLine($"Runtime: {runtimeMode}");
        Console.WriteLine($"State: {health.State.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Cards: {graph.Cards.Count}");
        Console.WriteLine($"Dispatchable tasks: {dispatch.ReadyTaskCount}");
        Console.WriteLine($"Current task: {session?.CurrentTaskId ?? "(none)"}");
        Console.WriteLine($"Next dispatchable task: {dispatch.NextTaskId ?? "(none)"}");
        Console.WriteLine($"Worker: {workerStatus}");
        Console.WriteLine($"Idle reason: {services.DispatchProjectionService.DescribeIdleReason(dispatch.IdleReason)}");
        if (!string.IsNullOrWhiteSpace(manifest?.RepoSummary))
        {
            Console.WriteLine($"Summary: {manifest.RepoSummary}");
        }

        Console.WriteLine();
        Console.WriteLine("Minimum onboarding:");
        Console.WriteLine($"  {RuntimeMinimumOnboardingGuidance.FriendlyReadSequence}");
        Console.WriteLine();
        Console.WriteLine("Next:");
        Console.WriteLine($"  {nextAction}");
        RenderFriendlyFeedbackGuidance(repoRoot, hostProjection.HostRunning, File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json")), health.State, dispatch.State);
    }

    private static string ResolveNextAction(FriendlyHostProjection hostProjection, RepoRuntimeHealthState healthState, DispatchProjection dispatch)
    {
        if (!hostProjection.HostRunning)
        {
            return ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json");
        }

        return healthState switch
        {
            RepoRuntimeHealthState.Broken or RepoRuntimeHealthState.Dirty => "carves maintain repair",
            _ when string.Equals(dispatch.State, "dispatchable", StringComparison.OrdinalIgnoreCase) => "carves run",
            _ => "carves status",
        };
    }

    private static void RenderFriendlyFeedbackGuidance(
        string repoRoot,
        bool hostConnected,
        bool runtimePresent,
        RepoRuntimeHealthState healthState,
        string dispatchState,
        bool useErrorStream = false)
    {
        var bundleId = RuntimeAgentOperatorFeedbackClosureService.SelectBundleId(hostConnected, runtimePresent, healthState, dispatchState);
        var bundle = RuntimeAgentOperatorFeedbackClosureService.GetFeedbackBundles()
            .FirstOrDefault(item => string.Equals(item.BundleId, bundleId, StringComparison.Ordinal));
        if (bundle is null || bundle.Commands.Count == 0)
        {
            return;
        }

        var writer = useErrorStream ? Console.Error : Console.Out;

        writer.WriteLine();
        writer.WriteLine("Guidance:");
        foreach (var command in bundle.Commands)
        {
            writer.WriteLine($"  {command}");
        }
    }

    private static int ResolveOptionalPositiveInt(IReadOnlyList<string> arguments, string option, int defaultValue, bool allowZero = false)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || parsed < 0 || (!allowZero && parsed == 0))
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a {(allowZero ? "non-negative" : "positive")} integer.");
            }

            return parsed;
        }

        return defaultValue;
    }

    private static int Fail(string message, int exitCode = 1)
    {
        Console.Error.WriteLine(message);
        return exitCode;
    }

    private static string? ResolveExternalRuntimeAuthorityRoot(string targetRepoRoot, string? runtimeRootOverride)
    {
        var candidate = !string.IsNullOrWhiteSpace(runtimeRootOverride)
            ? runtimeRootOverride
            : Environment.GetEnvironmentVariable(RuntimeRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(candidate);
        if (!Directory.Exists(fullPath) || PathEquals(fullPath, targetRepoRoot))
        {
            return null;
        }

        return fullPath;
    }

    private static bool PathEquals(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);
    }
}
