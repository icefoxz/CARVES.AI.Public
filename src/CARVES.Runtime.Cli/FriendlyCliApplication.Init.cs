using System.Text.Json;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunInit(string? explicitRepoRoot, string? runtimeRootOverride, IReadOnlyList<string> arguments)
    {
        var wantsJson = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var unknownOption = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--", StringComparison.Ordinal)
            && !string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var pathArguments = arguments
            .Where(argument => !argument.StartsWith("--", StringComparison.Ordinal))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(unknownOption) || pathArguments.Length > 1)
        {
            var readiness = InitReadiness.UsageError(
                workingDirectory: Directory.GetCurrentDirectory(),
                targetPath: explicitRepoRoot ?? pathArguments.FirstOrDefault());
            return RenderInitResult(readiness, wantsJson, exitCode: 2);
        }

        var target = ResolveInitTarget(explicitRepoRoot, pathArguments.FirstOrDefault());
        var targetRepo = ResolveTargetRepoState(target.TargetExists, target.IsWorkspace);
        var targetRepoReadiness = ResolveTargetRepoReadiness(
            target.TargetExists,
            target.IsWorkspace,
            target.RepoRoot is not null && Directory.Exists(Path.Combine(target.RepoRoot, ".ai")),
            target.RepoRoot is not null && File.Exists(Path.Combine(target.RepoRoot, ".ai", "runtime.json")));

        if (!target.TargetExists || !target.IsWorkspace || string.IsNullOrWhiteSpace(target.RepoRoot))
        {
            var readiness = new InitReadiness(
                SchemaVersion: "carves-init.v1",
                ToolReadiness: "available",
                CommandEntry: "carves",
                CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                WorkingDirectory: Directory.GetCurrentDirectory(),
                TargetPath: target.TargetPath,
                TargetRepo: targetRepo,
                TargetRepoPath: null,
                TargetRepoReadiness: targetRepoReadiness,
                RuntimeReadinessBefore: "not_checked",
                RuntimeReadinessAfter: "not_checked",
                HostReadiness: "not_checked",
                Action: "no_changes",
                NextAction: "run git init or pass --repo-root <path>",
                IsInitialized: false,
                Gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, "not_checked", "no_changes"),
                Commands: BuildInitCommands("target_missing_or_not_repository", target.TargetPath, null));

            return RenderInitResult(readiness, wantsJson, exitCode: 1);
        }

        var runtimeBefore = File.Exists(Path.Combine(target.RepoRoot, ".ai", "runtime.json"))
            ? "initialized"
            : "missing";

        var runtimeAuthorityRoot = ResolveExternalRuntimeAuthorityRoot(target.RepoRoot, runtimeRootOverride);
        if (!string.IsNullOrWhiteSpace(runtimeAuthorityRoot))
        {
            return RunInitThroughRuntimeAuthority(target, targetRepo, targetRepoReadiness, runtimeBefore, runtimeAuthorityRoot, wantsJson);
        }

        var hostProjection = ResolveFriendlyHostProjection(target.RepoRoot, "attach-flow");
        if (!hostProjection.HostRunning)
        {
            var readiness = new InitReadiness(
                SchemaVersion: "carves-init.v1",
                ToolReadiness: "available",
                CommandEntry: "carves",
                CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                WorkingDirectory: Directory.GetCurrentDirectory(),
                TargetPath: target.TargetPath,
                TargetRepo: targetRepo,
                TargetRepoPath: target.RepoRoot,
                TargetRepoReadiness: targetRepoReadiness,
                RuntimeReadinessBefore: runtimeBefore,
                RuntimeReadinessAfter: runtimeBefore,
                HostReadiness: hostProjection.HostReadiness,
                Action: "no_changes",
                NextAction: ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json"),
                IsInitialized: string.Equals(runtimeBefore, "initialized", StringComparison.Ordinal),
                Gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, hostProjection.HostReadiness, "no_changes"),
                Commands: BuildInitCommands(
                    hostProjection.ConflictPresent ? "host_session_conflict" : "host_not_running",
                    target.TargetPath,
                    target.RepoRoot,
                    ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json")));

            return RenderInitResult(readiness, wantsJson, exitCode: 1);
        }

        var attach = HostProgramInvoker.Invoke(target.RepoRoot, "attach");
        var runtimeAfter = File.Exists(Path.Combine(target.RepoRoot, ".ai", "runtime.json"))
            ? "initialized"
            : "missing";
        if (attach.ExitCode != 0)
        {
            var readiness = new InitReadiness(
                SchemaVersion: "carves-init.v1",
                ToolReadiness: "available",
                CommandEntry: "carves",
                CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                WorkingDirectory: Directory.GetCurrentDirectory(),
                TargetPath: target.TargetPath,
                TargetRepo: targetRepo,
                TargetRepoPath: target.RepoRoot,
                TargetRepoReadiness: targetRepoReadiness,
                RuntimeReadinessBefore: runtimeBefore,
                RuntimeReadinessAfter: runtimeAfter,
                HostReadiness: "connected",
                Action: "attach_failed",
                NextAction: "carves doctor",
                IsInitialized: string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal),
                Gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, "connected", "attach_failed"),
                Commands: BuildInitCommands("attach_failed", target.TargetPath, target.RepoRoot));

            if (!wantsJson)
            {
                attach.WriteToConsole();
            }

            return RenderInitResult(readiness, wantsJson, exitCode: attach.ExitCode);
        }

        var action = string.Equals(runtimeBefore, "initialized", StringComparison.Ordinal)
            ? "attached_existing_runtime"
            : "initialized_runtime";
        var success = new InitReadiness(
            SchemaVersion: "carves-init.v1",
            ToolReadiness: "available",
            CommandEntry: "carves",
            CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            WorkingDirectory: Directory.GetCurrentDirectory(),
            TargetPath: target.TargetPath,
            TargetRepo: targetRepo,
            TargetRepoPath: target.RepoRoot,
            TargetRepoReadiness: "runtime_initialized",
            RuntimeReadinessBefore: runtimeBefore,
            RuntimeReadinessAfter: runtimeAfter,
            HostReadiness: "connected",
            Action: action,
            NextAction: "carves doctor",
            IsInitialized: string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal),
            Gaps: [],
            Commands: BuildInitCommands("initialized", target.TargetPath, target.RepoRoot));

        return RenderInitResult(success, wantsJson, exitCode: success.IsInitialized ? 0 : 1);
    }

    private static int RunInitThroughRuntimeAuthority(
        InitTarget target,
        string targetRepo,
        string targetRepoReadiness,
        string runtimeBefore,
        string runtimeAuthorityRoot,
        bool wantsJson)
    {
        var attach = HostProgramInvoker.Invoke(
            runtimeAuthorityRoot,
            "attach",
            target.RepoRoot!,
            "--client-repo-root",
            target.RepoRoot!);
        var runtimeAfter = File.Exists(Path.Combine(target.RepoRoot!, ".ai", "runtime.json"))
            ? "initialized"
            : "missing";
        if (attach.ExitCode != 0)
        {
            var failed = new InitReadiness(
                SchemaVersion: "carves-init.v1",
                ToolReadiness: "available",
                CommandEntry: "carves",
                CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                WorkingDirectory: Directory.GetCurrentDirectory(),
                TargetPath: target.TargetPath,
                TargetRepo: targetRepo,
                TargetRepoPath: target.RepoRoot,
                TargetRepoReadiness: targetRepoReadiness,
                RuntimeReadinessBefore: runtimeBefore,
                RuntimeReadinessAfter: runtimeAfter,
                HostReadiness: "not_required_wrapper_runtime_root",
                Action: "attach_failed",
                NextAction: "carves doctor",
                IsInitialized: string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal),
                Gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, "connected", "attach_failed"),
                Commands: BuildInitCommands("attach_failed", target.TargetPath, target.RepoRoot));

            if (!wantsJson)
            {
                attach.WriteToConsole();
            }

            return RenderInitResult(failed, wantsJson, exitCode: attach.ExitCode);
        }

        var action = string.Equals(runtimeBefore, "initialized", StringComparison.Ordinal)
            ? "attached_existing_runtime"
            : "initialized_runtime";
        var success = new InitReadiness(
            SchemaVersion: "carves-init.v1",
            ToolReadiness: "available",
            CommandEntry: "carves",
            CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            WorkingDirectory: Directory.GetCurrentDirectory(),
            TargetPath: target.TargetPath,
            TargetRepo: targetRepo,
            TargetRepoPath: target.RepoRoot,
            TargetRepoReadiness: "runtime_initialized",
            RuntimeReadinessBefore: runtimeBefore,
            RuntimeReadinessAfter: runtimeAfter,
            HostReadiness: "not_required_wrapper_runtime_root",
            Action: action,
            NextAction: "carves doctor",
            IsInitialized: string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal),
            Gaps: [],
            Commands: BuildInitCommands("initialized", target.TargetPath, target.RepoRoot));

        return RenderInitResult(success, wantsJson, exitCode: success.IsInitialized ? 0 : 1);
    }

    private static InitTarget ResolveInitTarget(string? explicitRepoRoot, string? targetArgument)
    {
        var requestedPath = explicitRepoRoot ?? targetArgument ?? Directory.GetCurrentDirectory();
        var targetPath = Path.GetFullPath(requestedPath);
        if (!Directory.Exists(targetPath))
        {
            return new InitTarget(targetPath, TargetExists: false, RepoRoot: null, IsWorkspace: false);
        }

        var repoRoot = !string.IsNullOrWhiteSpace(explicitRepoRoot)
            ? RepoLocator.Resolve(explicitRepoRoot)
            : RepoLocator.Resolve(null, targetPath);
        var isWorkspace = !string.IsNullOrWhiteSpace(repoRoot) && RepoLocator.IsRepositoryWorkspace(repoRoot);
        return new InitTarget(targetPath, TargetExists: true, RepoRoot: repoRoot, IsWorkspace: isWorkspace);
    }

    private static IReadOnlyList<string> ResolveInitGaps(
        string targetRepo,
        string targetRepoReadiness,
        string hostReadiness,
        string action)
    {
        var gaps = new List<string>();
        if (string.Equals(targetRepo, "not_found", StringComparison.Ordinal))
        {
            gaps.Add("target_repo_not_found");
        }
        else if (string.Equals(targetRepo, "not_repository_workspace", StringComparison.Ordinal))
        {
            gaps.Add("target_repo_not_repository_workspace");
        }

        if (string.Equals(hostReadiness, "host_session_conflict", StringComparison.Ordinal))
        {
            if (!string.Equals(targetRepoReadiness, "runtime_initialized", StringComparison.Ordinal))
            {
                gaps.Add(targetRepoReadiness);
            }

            gaps.Add("resident_host_session_conflict");
        }
        else if (string.Equals(hostReadiness, "not_running", StringComparison.Ordinal))
        {
            if (!string.Equals(targetRepoReadiness, "runtime_initialized", StringComparison.Ordinal))
            {
                gaps.Add(targetRepoReadiness);
            }

            gaps.Add("resident_host_not_running");
        }

        if (string.Equals(action, "attach_failed", StringComparison.Ordinal))
        {
            gaps.Add("attach_failed");
        }

        return gaps;
    }

    private static IReadOnlyList<string> BuildInitCommands(string state, string targetPath, string? repoRoot, string? hostRecommendedAction = null)
    {
        return state switch
        {
            "target_missing_or_not_repository" =>
            [
                "git init",
                $"carves init {FormatCommandPath(targetPath)}",
            ],
            "host_not_running" =>
            [
                ResolveFriendlyHostNextAction(
                    new FriendlyHostProjection("not_running", "not_running", false, true, "ensure_host", hostRecommendedAction ?? string.Empty, "recoverable", "not_running", string.Empty),
                    "carves host ensure --json"),
                $"carves init {FormatCommandPath(repoRoot ?? targetPath)}",
            ],
            "host_session_conflict" =>
            [
                string.IsNullOrWhiteSpace(hostRecommendedAction) ? "carves host reconcile --replace-stale --json" : hostRecommendedAction,
                $"carves init {FormatCommandPath(repoRoot ?? targetPath)}",
                "carves doctor",
            ],
            "attach_failed" =>
            [
                "carves doctor",
                "carves host status",
                $"carves init {FormatCommandPath(repoRoot ?? targetPath)}",
            ],
            _ =>
            [
                "carves doctor",
                "carves agent handoff",
                "carves inspect runtime-first-run-operator-packet",
                "carves plan init [candidate-card-id]",
            ],
        };
    }

    private static string FormatCommandPath(string path)
    {
        return path.Any(char.IsWhiteSpace)
            ? $"\"{path}\""
            : path;
    }

    private static int RenderInitResult(InitReadiness readiness, bool wantsJson, int exitCode)
    {
        if (wantsJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(readiness, DoctorJsonOptions));
            return exitCode;
        }

        RenderInit(readiness);
        return exitCode;
    }

    private static void RenderInit(InitReadiness readiness)
    {
        Console.WriteLine("CARVES init");
        Console.WriteLine($"Tool readiness: {readiness.ToolReadiness}");
        Console.WriteLine($"Command entry: {readiness.CommandEntry}");
        Console.WriteLine($"CLI version: {readiness.CliVersion}");
        Console.WriteLine($"Working directory: {readiness.WorkingDirectory}");
        Console.WriteLine($"Target path: {readiness.TargetPath}");
        Console.WriteLine($"Target repo: {readiness.TargetRepo}");
        Console.WriteLine($"Target repo path: {readiness.TargetRepoPath ?? "(none)"}");
        Console.WriteLine($"Target repo readiness: {readiness.TargetRepoReadiness}");
        Console.WriteLine($"Runtime readiness before: {readiness.RuntimeReadinessBefore}");
        Console.WriteLine($"Runtime readiness after: {readiness.RuntimeReadinessAfter}");
        Console.WriteLine($"Host readiness: {readiness.HostReadiness}");
        Console.WriteLine($"Action: {readiness.Action}");
        Console.WriteLine($"Initialized: {readiness.IsInitialized}");
        Console.WriteLine();
        Console.WriteLine("Boundary:");
        Console.WriteLine("- init binds an existing git/workspace repo through the existing Runtime attach flow.");
        Console.WriteLine("- init does not create business goals, cards, tasks, or acceptance contracts.");
        Console.WriteLine("- durable planning still starts through one active formal planning card.");
        Console.WriteLine();
        Console.WriteLine("Gaps:");
        if (readiness.Gaps.Count == 0)
        {
            Console.WriteLine("- none");
        }
        else
        {
            foreach (var gap in readiness.Gaps)
            {
                Console.WriteLine($"- {gap}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Next:");
        Console.WriteLine($"  {readiness.NextAction}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (var command in readiness.Commands)
        {
            Console.WriteLine($"  {command}");
        }
    }

    private sealed record InitTarget(string TargetPath, bool TargetExists, string? RepoRoot, bool IsWorkspace);

    private sealed record InitReadiness(
        string SchemaVersion,
        string ToolReadiness,
        string CommandEntry,
        string CliVersion,
        string WorkingDirectory,
        string? TargetPath,
        string TargetRepo,
        string? TargetRepoPath,
        string TargetRepoReadiness,
        string RuntimeReadinessBefore,
        string RuntimeReadinessAfter,
        string HostReadiness,
        string Action,
        string NextAction,
        bool IsInitialized,
        IReadOnlyList<string> Gaps,
        IReadOnlyList<string> Commands)
    {
        public static InitReadiness UsageError(string workingDirectory, string? targetPath)
        {
            return new InitReadiness(
                SchemaVersion: "carves-init.v1",
                ToolReadiness: "available",
                CommandEntry: "carves",
                CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                WorkingDirectory: workingDirectory,
                TargetPath: targetPath,
                TargetRepo: "not_checked",
                TargetRepoPath: null,
                TargetRepoReadiness: "not_checked",
                RuntimeReadinessBefore: "not_checked",
                RuntimeReadinessAfter: "not_checked",
                HostReadiness: "not_checked",
                Action: "usage_error",
                NextAction: "carves init [path] [--json]",
                IsInitialized: false,
                Gaps: ["usage_error"],
                Commands: ["carves init [path] [--json]"]);
        }
    }
}
