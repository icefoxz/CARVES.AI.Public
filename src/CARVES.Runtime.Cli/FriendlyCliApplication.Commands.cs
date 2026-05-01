using Carves.Runtime.Host;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunAttach(string repoRoot, string? runtimeRootOverride)
    {
        var runtimeAuthorityRoot = ResolveExternalRuntimeAuthorityRoot(repoRoot, runtimeRootOverride);
        if (!string.IsNullOrWhiteSpace(runtimeAuthorityRoot))
        {
            return RunAttachThroughRuntimeAuthority(repoRoot, runtimeAuthorityRoot);
        }

        var projectName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var hadRuntime = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var hadAiDirectory = Directory.Exists(Path.Combine(repoRoot, ".ai"));
        var hostProjection = ResolveFriendlyHostProjection(repoRoot, "attach-flow");
        if (!hostProjection.HostRunning)
        {
            Console.Error.WriteLine($"Project: {projectName}");
            Console.Error.WriteLine($"Host: {DescribeFriendlyHostLabel(hostProjection)}");
            Console.Error.WriteLine($"Host operational state: {hostProjection.HostOperationalState}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Minimum onboarding:");
            Console.Error.WriteLine($"  {RuntimeMinimumOnboardingGuidance.FriendlyReadSequence}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Next:");
            Console.Error.WriteLine($"  {ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json")}");
            RenderFriendlyFeedbackGuidance(repoRoot, hostConnected: false, runtimePresent: false, RepoRuntimeHealthState.Healthy, "host_not_running", useErrorStream: true);
            return 1;
        }

        if (!hadAiDirectory || !hadRuntime)
        {
            Console.WriteLine("Runtime not found.");
            Console.WriteLine();
            Console.WriteLine("Initializing .ai/ ...");
            Console.WriteLine();
        }

        var attach = HostProgramInvoker.Invoke(repoRoot, "attach");
        if (attach.ExitCode != 0)
        {
            attach.WriteToConsole();
            return attach.ExitCode;
        }

        RenderFriendlySummary(repoRoot, runtimeMode: hadRuntime ? "attached" : "initialized", hostProjection);
        return 0;
    }

    private static int RunAttachThroughRuntimeAuthority(string repoRoot, string runtimeAuthorityRoot)
    {
        var projectName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var hadRuntime = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var hadAiDirectory = Directory.Exists(Path.Combine(repoRoot, ".ai"));

        if (!hadAiDirectory || !hadRuntime)
        {
            Console.WriteLine("Runtime not found.");
            Console.WriteLine();
            Console.WriteLine("Initializing .ai/ through wrapper Runtime root ...");
            Console.WriteLine();
        }

        var attach = HostProgramInvoker.Invoke(
            runtimeAuthorityRoot,
            "attach",
            repoRoot,
            "--client-repo-root",
            repoRoot);
        if (attach.ExitCode != 0)
        {
            attach.WriteToConsole();
            return attach.ExitCode;
        }

        var hostProjection = ResolveFriendlyHostProjection(runtimeAuthorityRoot);
        RenderFriendlySummary(repoRoot, runtimeMode: hadRuntime ? "attached" : "initialized", hostProjection);
        if (!hostProjection.HostRunning)
        {
            Console.WriteLine();
            Console.WriteLine($"Project: {projectName}");
            Console.WriteLine($"Runtime authority root: {runtimeAuthorityRoot}");
            Console.WriteLine("Host: not required for attach; cold wrapper attach completed.");
        }

        return 0;
    }

    private static int RunStatus(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        var watch = arguments.Any(argument => string.Equals(argument, "--watch", StringComparison.OrdinalIgnoreCase));
        var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", defaultValue: 1500, allowZero: true);
        var iterations = ResolveOptionalPositiveInt(arguments, "--iterations", defaultValue: 0, allowZero: true);
        var filtered = FilterStatusArguments(arguments);
        if (watch)
        {
            return RunStatusWatch(repoRoot, filtered, transport, intervalMilliseconds, iterations);
        }

        return RunStatusOnce(repoRoot, filtered, transport);
    }

    private static int RunStatusOnce(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count > 0)
        {
            return Delegate(repoRoot, "status", arguments, transport);
        }

        var hostProjection = ResolveFriendlyHostProjection(repoRoot);
        var runtimeMode = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json")) ? "initialized" : "missing";
        if (!hostProjection.HostRunning)
        {
            RenderFriendlySummary(repoRoot, runtimeMode, hostProjection);
            return 1;
        }

        RenderFriendlySummary(repoRoot, runtimeMode, hostProjection);
        return 0;
    }

    private static int RunStatusWatch(
        string repoRoot,
        IReadOnlyList<string> arguments,
        TransportPreference transport,
        int intervalMilliseconds,
        int iterations)
    {
        var cycle = 0;
        while (iterations == 0 || cycle < iterations)
        {
            if (cycle > 0)
            {
                Console.WriteLine();
            }

            Console.WriteLine($"=== CARVES Status {DateTimeOffset.Now:O} ===");
            var exitCode = RunStatusOnce(repoRoot, arguments, transport);
            if (exitCode != 0)
            {
                return exitCode;
            }

            cycle += 1;
            if (iterations > 0 && cycle >= iterations)
            {
                break;
            }

            if (intervalMilliseconds > 0)
            {
                DelayCliWatch(intervalMilliseconds);
            }
        }

        return 0;
    }

    private static int RunInspect(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine("Usage: carves inspect <card|task|taskgraph|review|audit|packet> ...");
            return 2;
        }

        if (arguments.Count == 1)
        {
            return Delegate(repoRoot, "inspect", arguments, transport);
        }

        return Delegate(repoRoot, "inspect", arguments, transport);
    }

    private static int RunPlan(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine("Usage: carves plan <card|draft-card|approve-card|draft-taskgraph|approve-taskgraph> ...");
            return 2;
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "status" or "init" or "packet" or "issue-workspace" or "submit-workspace" or "export-card" or "export-packet"
                => Delegate(repoRoot, "plan", arguments, transport),
            "card" when arguments.Count >= 2 => Delegate(repoRoot, "plan-card", arguments.Skip(1).ToArray(), transport),
            "draft-card" when arguments.Count >= 2 => Delegate(repoRoot, "create-card-draft", arguments.Skip(1).ToArray(), transport),
            "approve-card" when arguments.Count >= 2 => Delegate(repoRoot, "approve-card", arguments.Skip(1).ToArray(), transport),
            "draft-taskgraph" when arguments.Count >= 2 => Delegate(repoRoot, "create-taskgraph-draft", arguments.Skip(1).ToArray(), transport),
            "approve-taskgraph" when arguments.Count >= 2 => Delegate(repoRoot, "approve-taskgraph-draft", arguments.Skip(1).ToArray(), transport),
            _ => Fail("Usage: carves plan <card|draft-card|approve-card|draft-taskgraph|approve-taskgraph> ...", 2),
        };
    }

    private static int RunRun(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            return RunRunNext(repoRoot, Array.Empty<string>(), transport);
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "next" => RunRunNext(repoRoot, arguments.Skip(1).ToArray(), transport),
            "task" when arguments.Count >= 2 => Delegate(repoRoot, "task", ["run", arguments[1], .. arguments.Skip(2)], transport),
            "retry" when arguments.Count >= 2 => Delegate(repoRoot, "task", ["retry", arguments[1], .. arguments.Skip(2)], transport),
            "resume" when arguments.Count >= 3 && string.Equals(arguments[1], "task", StringComparison.OrdinalIgnoreCase)
                => Delegate(repoRoot, "task", ["run", arguments[2], .. arguments.Skip(3)], transport),
            _ => Fail("Usage: carves run [next|task <task-id>|retry <task-id>|resume task <task-id>] [...]", 2),
        };
    }

    private static int RunRunNext(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (transport == TransportPreference.Host)
        {
            return EnsureHostAndDelegate(repoRoot, "run-next", arguments);
        }

        if (transport == TransportPreference.Cold)
        {
            return Delegate(repoRoot, "run-next", arguments, transport);
        }

        var hostProjection = ResolveFriendlyHostProjection(repoRoot);
        if (!hostProjection.HostRunning)
        {
            return RenderFriendlyHostTransportFailure(hostProjection, exitCode: 1);
        }

        return Delegate(repoRoot, "run-next", arguments, transport);
    }

    private static int RunReview(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine("Usage: carves review <approve|reject|reopen|block|supersede|task> ...");
            return 2;
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "approve" when arguments.Count >= 3 => Delegate(repoRoot, "approve-review", arguments.Skip(1).ToArray(), transport),
            "reject" when arguments.Count >= 3 => Delegate(repoRoot, "reject-review", arguments.Skip(1).ToArray(), transport),
            "reopen" when arguments.Count >= 3 => Delegate(repoRoot, "reopen-review", arguments.Skip(1).ToArray(), transport),
            "block" or "blocked" when arguments.Count >= 3 => Delegate(repoRoot, "review-task", [arguments[1], "blocked", .. arguments.Skip(2)], transport),
            "supersede" or "superseded" when arguments.Count >= 3 => Delegate(repoRoot, "review-task", [arguments[1], "superseded", .. arguments.Skip(2)], transport),
            "task" when arguments.Count >= 4 => Delegate(repoRoot, "review-task", arguments.Skip(1).ToArray(), transport),
            _ => Fail("Usage: carves review <approve|reject|reopen|block|supersede|task> ...", 2),
        };
    }

    private static int RunAudit(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine("Usage: carves audit <summary|timeline|explain|evidence|sustainability|codegraph|runtime-noise> [...]");
            return 2;
        }

        if (IsPublicAuditProductCommand(arguments[0]))
        {
            return Carves.Audit.Core.AuditCliRunner.Run(repoRoot, arguments);
        }

        return Delegate(repoRoot, "audit", arguments, transport);
    }

    private static bool IsPublicAuditProductCommand(string command)
    {
        return command.ToLowerInvariant() is "summary" or "timeline" or "explain" or "evidence" or "help" or "--help" or "-h";
    }

    private static int RunMaintain(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine("Usage: carves maintain <compact-history|cleanup|detect-refactors|repair|rebuild> [...]");
            return 2;
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "compact-history" => Delegate(repoRoot, "compact-history", arguments.Skip(1).ToArray(), transport),
            "cleanup" => Delegate(repoRoot, "cleanup", arguments.Skip(1).ToArray(), transport),
            "detect-refactors" => Delegate(repoRoot, "detect-refactors", arguments.Skip(1).ToArray(), transport),
            "repair" => Delegate(repoRoot, "repair", arguments.Skip(1).ToArray(), transport),
            "rebuild" or "rebuild-codegraph" => Delegate(repoRoot, "rebuild", arguments.Skip(1).ToArray(), transport),
            _ => Fail("Usage: carves maintain <compact-history|cleanup|detect-refactors|repair|rebuild> [...]", 2),
        };
    }

    private static int RunWorkbench(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        var watch = arguments.Any(argument => string.Equals(argument, "--watch", StringComparison.OrdinalIgnoreCase));
        var intervalMilliseconds = ResolveOptionalPositiveInt(arguments, "--interval-ms", defaultValue: 1500, allowZero: true);
        var iterations = ResolveOptionalPositiveInt(arguments, "--iterations", defaultValue: 0, allowZero: true);
        var filtered = FilterWorkbenchArguments(arguments);
        if (watch)
        {
            return RunWorkbenchWatch(repoRoot, filtered, transport, intervalMilliseconds, iterations);
        }

        return Delegate(repoRoot, "workbench", filtered, transport);
    }

    private static IReadOnlyList<string> FilterStatusArguments(IReadOnlyList<string> arguments)
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
}
