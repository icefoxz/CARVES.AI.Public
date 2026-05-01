using System.Text.Json;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private const string UpAgentStartCommand = ".carves/carves agent start --json";
    private const string UpAgentStartPrompt = "start CARVES";
    private const string UpAgentInstruction = "read .carves/AGENT_START.md, then run .carves/carves agent start --json";
    private const string UpAgentStartCopyPastePrompt = """
start CARVES

Read CARVES_START.md first. Then run .carves/carves agent start --json from this project and follow CARVES output. Do not plan or edit before that readback.
""";

    private static int RunUp(string? explicitRepoRoot, string? runtimeRootOverride, IReadOnlyList<string> arguments)
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
            var usage = BuildUpReadiness(
                targetPath: explicitRepoRoot ?? pathArguments.FirstOrDefault() ?? Directory.GetCurrentDirectory(),
                targetRepo: "not_checked",
                targetRepoPath: null,
                targetRepoReadiness: "usage_error",
                runtimeReadinessBefore: "not_checked",
                runtimeReadinessAfter: "not_checked",
                runtimeAuthorityRoot: runtimeRootOverride,
                hostAuthorityRoot: null,
                hostReadiness: "not_checked",
                hostAutoEnsured: false,
                action: "usage_error",
                nextAction: "carves up [target-project] [--json]",
                isReady: false,
                gaps: ["usage_error"],
                commands: ["carves up [target-project] [--json]"],
                bootstrap: null);
            return RenderUpResult(usage, wantsJson, exitCode: 2);
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
            var invalid = BuildUpReadiness(
                target.TargetPath,
                targetRepo,
                null,
                targetRepoReadiness,
                runtimeReadinessBefore: "not_checked",
                runtimeReadinessAfter: "not_checked",
                runtimeAuthorityRoot: runtimeRootOverride,
                hostAuthorityRoot: null,
                hostReadiness: "not_checked",
                hostAutoEnsured: false,
                action: "no_changes",
                nextAction: "run git init or pass a git repository path",
                isReady: false,
                gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, "not_checked", "no_changes"),
                commands: ["git init", $"carves up {FormatCommandPath(target.TargetPath)}"],
                bootstrap: null);
            return RenderUpResult(invalid, wantsJson, exitCode: 1);
        }

        var runtimeBefore = File.Exists(Path.Combine(target.RepoRoot, ".ai", "runtime.json"))
            ? "initialized"
            : "missing";
        var runtimeAuthorityRoot = ResolveUpRuntimeAuthorityRoot(target.RepoRoot, runtimeRootOverride);
        if (string.IsNullOrWhiteSpace(runtimeAuthorityRoot))
        {
            var blocked = BuildUpReadiness(
                target.TargetPath,
                targetRepo,
                target.RepoRoot,
                targetRepoReadiness,
                runtimeBefore,
                runtimeBefore,
                runtimeAuthorityRoot: null,
                hostAuthorityRoot: null,
                hostReadiness: "not_checked",
                hostAutoEnsured: false,
                action: "blocked_runtime_authority_missing",
                nextAction: "run from the CARVES.Runtime folder or pass --runtime-root <path>",
                isReady: false,
                gaps: ["runtime_authority_root_missing"],
                commands: [$"carves up {FormatCommandPath(target.TargetPath)} --runtime-root <path-to-CARVES.Runtime>"],
                bootstrap: null);
            return RenderUpResult(blocked, wantsJson, exitCode: 1);
        }

        var runtimeBinding = RuntimeTargetBindingReadbackResolver.Resolve(
            target.RepoRoot,
            runtimeAuthorityRoot,
            "runtime_authority");
        if (string.Equals(runtimeBefore, "initialized", StringComparison.Ordinal)
            && runtimeBinding.BlocksAgentStartup)
        {
            var blocked = BuildUpReadiness(
                target.TargetPath,
                targetRepo,
                target.RepoRoot,
                targetRepoReadiness,
                runtimeBefore,
                runtimeBefore,
                runtimeAuthorityRoot,
                hostAuthorityRoot: null,
                hostReadiness: "not_checked",
                hostAutoEnsured: false,
                action: "rebind_required",
                nextAction: BuildUpRuntimeRebindNextAction(runtimeBinding),
                isReady: false,
                gaps: runtimeBinding.Gaps,
                commands: BuildUpRuntimeRebindCommands(runtimeBinding, target.TargetPath),
                bootstrap: null);
            return RenderUpResult(blocked, wantsJson, exitCode: 1);
        }

        var hostAuthorityRoot = runtimeAuthorityRoot;
        var hostAutoEnsured = false;
        var hostProjection = ResolveFriendlyHostProjection(hostAuthorityRoot, "attach-flow");
        if (!hostProjection.HostRunning)
        {
            if (hostProjection.ConflictPresent || !hostProjection.SafeToStartNewHost)
            {
                var blocked = BuildUpReadiness(
                    target.TargetPath,
                    targetRepo,
                    target.RepoRoot,
                    targetRepoReadiness,
                    runtimeBefore,
                    runtimeBefore,
                    runtimeAuthorityRoot,
                    hostAuthorityRoot,
                    hostProjection.HostReadiness,
                    hostAutoEnsured,
                    action: hostProjection.ConflictPresent ? "blocked_host_session_conflict" : "blocked_host_not_safe_to_start",
                    nextAction: ResolveFriendlyHostNextAction(hostProjection, "carves host reconcile --replace-stale --json"),
                    isReady: false,
                    gaps: hostProjection.ConflictPresent ? ["resident_host_session_conflict"] : ["resident_host_not_safe_to_start"],
                    commands: [ResolveFriendlyHostNextAction(hostProjection, "carves host reconcile --replace-stale --json")],
                    bootstrap: null);
                return RenderUpResult(blocked, wantsJson, exitCode: 1);
            }

            var ensure = HostProgramInvoker.Invoke(
                hostAuthorityRoot,
                "host",
                "ensure",
                "--json",
                "--require-capability",
                "attach-flow");
            hostAutoEnsured = true;
            hostProjection = ResolveFriendlyHostProjection(hostAuthorityRoot, "attach-flow");
            if (ensure.ExitCode != 0 || !hostProjection.HostRunning)
            {
                var blocked = BuildUpReadiness(
                    target.TargetPath,
                    targetRepo,
                    target.RepoRoot,
                    targetRepoReadiness,
                    runtimeBefore,
                    runtimeBefore,
                    runtimeAuthorityRoot,
                    hostAuthorityRoot,
                    hostProjection.HostReadiness,
                    hostAutoEnsured,
                    action: hostProjection.ConflictPresent ? "blocked_host_session_conflict" : "blocked_host_ensure_failed",
                    nextAction: ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json"),
                    isReady: false,
                    gaps: hostProjection.ConflictPresent ? ["resident_host_session_conflict"] : ["resident_host_ensure_failed"],
                    commands: [ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json")],
                    bootstrap: null);
                return RenderUpResult(blocked, wantsJson, exitCode: 1);
            }
        }

        var hostReadiness = hostProjection.HostReadiness;
        if (string.Equals(runtimeBefore, "initialized", StringComparison.Ordinal))
        {
            return RenderBootstrapUpResult(
                target.TargetPath,
                targetRepo,
                target.RepoRoot,
                targetRepoReadiness: "runtime_initialized",
                runtimeBefore,
                runtimeAfter: runtimeBefore,
                runtimeAuthorityRoot,
                hostAuthorityRoot,
                hostReadiness,
                hostAutoEnsured,
                wantsJson);
        }

        var attach = PathEquals(runtimeAuthorityRoot, target.RepoRoot)
            ? HostProgramInvoker.Invoke(target.RepoRoot, "attach")
            : HostProgramInvoker.Invoke(
                runtimeAuthorityRoot,
                "attach",
                target.RepoRoot,
                "--client-repo-root",
                target.RepoRoot);
        var attachExitCode = attach.ExitCode;
        var runtimeAfter = File.Exists(Path.Combine(target.RepoRoot, ".ai", "runtime.json"))
            ? "initialized"
            : "missing";
        if (attachExitCode != 0)
        {
            var failed = BuildUpReadiness(
                target.TargetPath,
                targetRepo,
                target.RepoRoot,
                targetRepoReadiness,
                runtimeBefore,
                runtimeAfter,
                runtimeAuthorityRoot,
                hostAuthorityRoot,
                hostReadiness,
                hostAutoEnsured,
                action: "attach_failed",
                nextAction: "carves doctor",
                isReady: false,
                gaps: ResolveInitGaps(targetRepo, targetRepoReadiness, "connected", "attach_failed"),
                commands: BuildInitCommands("attach_failed", target.TargetPath, target.RepoRoot),
                bootstrap: null);
            return RenderUpResult(failed, wantsJson, exitCode: attachExitCode);
        }

        return RenderBootstrapUpResult(
            target.TargetPath,
            targetRepo,
            target.RepoRoot,
            targetRepoReadiness: string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal)
                ? "runtime_initialized"
                : targetRepoReadiness,
            runtimeBefore,
            runtimeAfter,
            runtimeAuthorityRoot,
            hostAuthorityRoot,
            hostReadiness,
            hostAutoEnsured,
            wantsJson);
    }

    private static int RenderBootstrapUpResult(
        string targetPath,
        string targetRepo,
        string targetRepoPath,
        string targetRepoReadiness,
        string runtimeBefore,
        string runtimeAfter,
        string runtimeAuthorityRoot,
        string hostAuthorityRoot,
        string hostReadiness,
        bool hostAutoEnsured,
        bool wantsJson)
    {
        var bootstrap = new RuntimeTargetAgentBootstrapPackService(targetRepoPath).Build(writeRequested: true);
        var isReady = bootstrap.IsValid
                      && string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal)
                      && bootstrap.ProjectLocalLauncherExists
                      && bootstrap.AgentStartMarkdownExists
                      && bootstrap.AgentStartJsonExists
                      && bootstrap.VisibleAgentStartExists;
        var gaps = new List<string>();
        if (!string.Equals(runtimeAfter, "initialized", StringComparison.Ordinal))
        {
            gaps.Add("runtime_not_initialized");
        }

        if (!bootstrap.ProjectLocalLauncherExists)
        {
            gaps.Add("project_local_launcher_missing");
        }

        if (!bootstrap.AgentStartMarkdownExists)
        {
            gaps.Add("agent_start_markdown_missing");
        }

        if (!bootstrap.AgentStartJsonExists)
        {
            gaps.Add("agent_start_json_missing");
        }

        if (!bootstrap.VisibleAgentStartExists)
        {
            gaps.Add("visible_agent_start_missing");
        }

        gaps.AddRange(bootstrap.Errors.Select(error => $"bootstrap:{error}"));

        var success = BuildUpReadiness(
            targetPath,
            targetRepo,
            targetRepoPath,
            targetRepoReadiness,
            runtimeBefore,
            runtimeAfter,
            runtimeAuthorityRoot,
            hostAuthorityRoot,
            hostReadiness,
            hostAutoEnsured,
            action: isReady ? "ready_for_agent_start" : "bootstrap_incomplete",
            nextAction: isReady ? UpAgentStartCommand : bootstrap.RecommendedNextAction,
            isReady,
            gaps,
            commands: isReady
                ? [UpAgentStartCommand]
                : ["carves agent bootstrap --write", UpAgentStartCommand],
            bootstrap);
        return RenderUpResult(success, wantsJson, exitCode: isReady ? 0 : 1);
    }

    private static UpReadiness BuildUpReadiness(
        string targetPath,
        string targetRepo,
        string? targetRepoPath,
        string targetRepoReadiness,
        string runtimeReadinessBefore,
        string runtimeReadinessAfter,
        string? runtimeAuthorityRoot,
        string? hostAuthorityRoot,
        string hostReadiness,
        bool hostAutoEnsured,
        string action,
        string nextAction,
        bool isReady,
        IReadOnlyList<string> gaps,
        IReadOnlyList<string> commands,
        RuntimeTargetAgentBootstrapPackSurface? bootstrap)
    {
        var targetBinding = RuntimeTargetBindingReadbackResolver.Resolve(
            targetRepoPath,
            runtimeAuthorityRoot,
            "runtime_authority");
        return new UpReadiness(
            SchemaVersion: "carves-up.v1",
            ToolReadiness: "available",
            CommandEntry: "carves up",
            CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            WorkingDirectory: Directory.GetCurrentDirectory(),
            TargetPath: Path.GetFullPath(targetPath),
            TargetRepo: targetRepo,
            TargetRepoPath: targetRepoPath,
            TargetRepoReadiness: targetRepoReadiness,
            RuntimeReadinessBefore: runtimeReadinessBefore,
            RuntimeReadinessAfter: runtimeReadinessAfter,
            RuntimeAuthorityRoot: string.IsNullOrWhiteSpace(runtimeAuthorityRoot) ? null : Path.GetFullPath(runtimeAuthorityRoot),
            HostAuthorityRoot: string.IsNullOrWhiteSpace(hostAuthorityRoot) ? null : Path.GetFullPath(hostAuthorityRoot),
            HostReadiness: hostReadiness,
            HostAutoEnsured: hostAutoEnsured,
            TargetBoundRuntimeRoot: targetBinding.BoundRuntimeRoot,
            TargetRuntimeBindingStatus: targetBinding.Status,
            TargetRuntimeBindingSource: targetBinding.Source,
            AgentRuntimeRebindAllowed: false,
            TargetProjectClassification: ResolveUpTargetProjectClassification(
                targetRepo,
                targetRepoReadiness,
                runtimeReadinessBefore,
                runtimeReadinessAfter,
                action),
            TargetClassificationOwner: "carves_up",
            TargetClassificationSource: ResolveUpTargetClassificationSource(
                targetRepo,
                targetRepoReadiness,
                runtimeReadinessBefore,
                runtimeReadinessAfter,
                action),
            AgentTargetClassificationAllowed: false,
            TargetStartupMode: ResolveUpTargetStartupMode(
                targetRepo,
                targetRepoReadiness,
                runtimeReadinessBefore,
                runtimeReadinessAfter,
                action),
            ExistingProjectHandling: ResolveUpExistingProjectHandling(
                targetRepo,
                runtimeReadinessBefore,
                runtimeReadinessAfter,
                action),
            Action: action,
            ProjectLocalLauncher: RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath,
            ProjectLocalLauncherExists: bootstrap?.ProjectLocalLauncherExists ?? false,
            AgentStartMarkdown: RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath,
            AgentStartMarkdownExists: bootstrap?.AgentStartMarkdownExists ?? false,
            AgentStartJson: RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath,
            AgentStartJsonExists: bootstrap?.AgentStartJsonExists ?? false,
            VisibleAgentStart: RuntimeTargetAgentBootstrapPackService.VisibleAgentStartPath,
            VisibleAgentStartExists: bootstrap?.VisibleAgentStartExists ?? false,
            RootAgentsIntegrationPosture: bootstrap?.RootAgentsIntegrationPosture ?? string.Empty,
            RootAgentsSuggestedPatch: bootstrap?.RootAgentsSuggestedPatch ?? string.Empty,
            WorkerExecutionBoundary: "null_worker_current_version_no_api_sdk_worker_execution",
            AgentStartCommand: UpAgentStartCommand,
            HumanNextAction: BuildUpHumanNextAction(isReady, targetRepoPath, targetPath, nextAction),
            AgentStartPrompt: isReady ? UpAgentStartPrompt : string.Empty,
            AgentStartCopyPastePrompt: isReady ? UpAgentStartCopyPastePrompt : string.Empty,
            AgentInstruction: isReady ? UpAgentInstruction : string.Empty,
            NextAction: nextAction,
            IsReady: isReady,
            Gaps: gaps,
            Commands: commands,
            NonAuthority:
            [
                "not_dashboard_product_entry",
                "not_worker_execution_authority",
                "not_task_run",
                "not_review_approval",
                "not_state_sync",
                "not_global_alias_authority",
            ]);
    }

    private static int RenderUpResult(UpReadiness readiness, bool wantsJson, int exitCode)
    {
        if (wantsJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(readiness, DoctorJsonOptions));
            return exitCode;
        }

        Console.WriteLine("CARVES up");
        Console.WriteLine($"Target repo: {readiness.TargetRepo}");
        Console.WriteLine($"Target readiness: {readiness.TargetRepoReadiness}");
        Console.WriteLine($"Target classification: {readiness.TargetProjectClassification}");
        Console.WriteLine($"Target startup mode: {readiness.TargetStartupMode}");
        Console.WriteLine($"Target runtime binding: {readiness.TargetRuntimeBindingStatus}");
        Console.WriteLine($"Target bound Runtime root: {readiness.TargetBoundRuntimeRoot ?? "(none)"}");
        Console.WriteLine($"Runtime: {readiness.RuntimeReadinessAfter}");
        Console.WriteLine($"Host readiness: {readiness.HostReadiness}");
        Console.WriteLine($"Host auto-ensured: {readiness.HostAutoEnsured}");
        Console.WriteLine($"Action: {readiness.Action}");
        Console.WriteLine($"Project-local launcher: {readiness.ProjectLocalLauncher}");
        Console.WriteLine($"Visible start pointer: {readiness.VisibleAgentStart}");
        Console.WriteLine($"Agent start packet: {readiness.AgentStartMarkdown}");
        Console.WriteLine($"Worker boundary: {readiness.WorkerExecutionBoundary}");
        Console.WriteLine();
        Console.WriteLine("Next for you:");
        Console.WriteLine($"  {readiness.HumanNextAction}");
        if (readiness.IsReady)
        {
            Console.WriteLine();
            Console.WriteLine("Copy/paste to your agent:");
            foreach (var line in readiness.AgentStartCopyPastePrompt.Split('\n'))
            {
                Console.WriteLine($"  {line.TrimEnd('\r')}");
            }

            Console.WriteLine();
            Console.WriteLine("Next for the agent:");
            Console.WriteLine($"  {readiness.AgentInstruction}");
        }

        if (readiness.Gaps.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Gaps:");
            foreach (var gap in readiness.Gaps)
            {
                Console.WriteLine($"  - {gap}");
            }
        }

        return exitCode;
    }

    private sealed record UpReadiness(
        string SchemaVersion,
        string ToolReadiness,
        string CommandEntry,
        string CliVersion,
        string WorkingDirectory,
        string TargetPath,
        string TargetRepo,
        string? TargetRepoPath,
        string TargetRepoReadiness,
        string RuntimeReadinessBefore,
        string RuntimeReadinessAfter,
        string? RuntimeAuthorityRoot,
        string? HostAuthorityRoot,
        string HostReadiness,
        bool HostAutoEnsured,
        string? TargetBoundRuntimeRoot,
        string TargetRuntimeBindingStatus,
        string TargetRuntimeBindingSource,
        bool AgentRuntimeRebindAllowed,
        string TargetProjectClassification,
        string TargetClassificationOwner,
        string TargetClassificationSource,
        bool AgentTargetClassificationAllowed,
        string TargetStartupMode,
        string ExistingProjectHandling,
        string Action,
        string ProjectLocalLauncher,
        bool ProjectLocalLauncherExists,
        string AgentStartMarkdown,
        bool AgentStartMarkdownExists,
        string AgentStartJson,
        bool AgentStartJsonExists,
        string VisibleAgentStart,
        bool VisibleAgentStartExists,
        string RootAgentsIntegrationPosture,
        string RootAgentsSuggestedPatch,
        string WorkerExecutionBoundary,
        string AgentStartCommand,
        string HumanNextAction,
        string AgentStartPrompt,
        string AgentStartCopyPastePrompt,
        string AgentInstruction,
        string NextAction,
        bool IsReady,
        IReadOnlyList<string> Gaps,
        IReadOnlyList<string> Commands,
        IReadOnlyList<string> NonAuthority);

    private static string BuildUpRuntimeRebindNextAction(RuntimeTargetBindingReadback binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.BoundRuntimeRoot))
        {
            return $"operator decision required: rerun from bound Runtime root {FormatCommandPath(RuntimeCliWrapperPaths.PreferredWrapperPath(binding.BoundRuntimeRoot))} up <target-project>, or rebind through the governed Runtime binding flow";
        }

        return "operator decision required: inspect target .ai/runtime.json and rebind through the governed Runtime binding flow";
    }

    private static IReadOnlyList<string> BuildUpRuntimeRebindCommands(RuntimeTargetBindingReadback binding, string targetPath)
    {
        if (!string.IsNullOrWhiteSpace(binding.BoundRuntimeRoot))
        {
            return
            [
                $"{FormatCommandPath(RuntimeCliWrapperPaths.PreferredWrapperPath(binding.BoundRuntimeRoot))} up {FormatCommandPath(targetPath)}",
                "carves pilot dist-binding --json",
            ];
        }

        return
        [
            "carves pilot dist-binding --json",
            $"carves up {FormatCommandPath(targetPath)}",
        ];
    }

    private static string ResolveUpTargetProjectClassification(
        string targetRepo,
        string targetRepoReadiness,
        string runtimeReadinessBefore,
        string runtimeReadinessAfter,
        string action)
    {
        if (string.Equals(targetRepo, "not_found", StringComparison.Ordinal))
        {
            return "target_not_found";
        }

        if (string.Equals(targetRepo, "not_repository_workspace", StringComparison.Ordinal))
        {
            return "not_repository_workspace";
        }

        if (!string.Equals(targetRepo, "found", StringComparison.Ordinal))
        {
            return "not_classified";
        }

        if (string.Equals(runtimeReadinessBefore, "initialized", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "existing_carves_project";
        }

        if (string.Equals(runtimeReadinessBefore, "missing", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "newly_attached_git_project";
        }

        if (string.Equals(targetRepoReadiness, "partial_runtime_missing_manifest", StringComparison.Ordinal))
        {
            return "partial_carves_project_missing_manifest";
        }

        return string.Equals(action, "ready_for_agent_start", StringComparison.Ordinal)
            ? "attached_git_project"
            : "unattached_git_project";
    }

    private static string ResolveUpTargetClassificationSource(
        string targetRepo,
        string targetRepoReadiness,
        string runtimeReadinessBefore,
        string runtimeReadinessAfter,
        string action)
    {
        if (string.Equals(targetRepo, "not_found", StringComparison.Ordinal))
        {
            return "target_path_probe";
        }

        if (string.Equals(targetRepo, "not_repository_workspace", StringComparison.Ordinal))
        {
            return "git_workspace_probe";
        }

        if (string.Equals(runtimeReadinessBefore, "initialized", StringComparison.Ordinal))
        {
            return ".ai/runtime.json";
        }

        if (string.Equals(targetRepoReadiness, "partial_runtime_missing_manifest", StringComparison.Ordinal))
        {
            return ".ai_directory_probe";
        }

        if (string.Equals(runtimeReadinessBefore, "missing", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "git_repo_probe_and_attach_result";
        }

        return string.Equals(action, "ready_for_agent_start", StringComparison.Ordinal)
            ? "carves_up_readiness_result"
            : "git_repo_probe";
    }

    private static string ResolveUpTargetStartupMode(
        string targetRepo,
        string targetRepoReadiness,
        string runtimeReadinessBefore,
        string runtimeReadinessAfter,
        string action)
    {
        if (string.Equals(action, "rebind_required", StringComparison.Ordinal))
        {
            return "blocked_rebind_required";
        }

        if (!string.Equals(targetRepo, "found", StringComparison.Ordinal))
        {
            return "blocked_before_runtime_mutation";
        }

        if (string.Equals(runtimeReadinessBefore, "initialized", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "reuse_existing_runtime";
        }

        if (string.Equals(runtimeReadinessBefore, "missing", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "attach_missing_runtime";
        }

        if (string.Equals(targetRepoReadiness, "partial_runtime_missing_manifest", StringComparison.Ordinal))
        {
            return "blocked_partial_runtime";
        }

        return string.Equals(action, "ready_for_agent_start", StringComparison.Ordinal)
            ? "ready_existing_bootstrap"
            : "blocked_before_agent_start";
    }

    private static string ResolveUpExistingProjectHandling(
        string targetRepo,
        string runtimeReadinessBefore,
        string runtimeReadinessAfter,
        string action)
    {
        if (string.Equals(action, "rebind_required", StringComparison.Ordinal))
        {
            return "operator_rebind_required_agent_must_stop";
        }

        if (!string.Equals(targetRepo, "found", StringComparison.Ordinal))
        {
            return "not_applicable";
        }

        if (string.Equals(runtimeReadinessBefore, "initialized", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "preserved_existing_carves_project_no_reinit";
        }

        if (string.Equals(runtimeReadinessBefore, "missing", StringComparison.Ordinal)
            && string.Equals(runtimeReadinessAfter, "initialized", StringComparison.Ordinal))
        {
            return "attached_missing_runtime_without_agent_guessing";
        }

        return string.Equals(action, "ready_for_agent_start", StringComparison.Ordinal)
            ? "ready_without_agent_classification"
            : "blocked_before_existing_project_mutation";
    }

    private static string BuildUpHumanNextAction(bool isReady, string? targetRepoPath, string targetPath, string nextAction)
    {
        if (!isReady)
        {
            return nextAction;
        }

        var target = Path.GetFullPath(targetRepoPath ?? targetPath);
        return $"open {target} in your coding agent and say: {UpAgentStartPrompt}";
    }

    private static string? ResolveUpRuntimeAuthorityRoot(string targetRepoRoot, string? runtimeRootOverride)
    {
        var configured = ResolveExternalRuntimeAuthorityRoot(targetRepoRoot, runtimeRootOverride);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var currentRepoRoot = RepoLocator.Resolve(null, Directory.GetCurrentDirectory());
        if (!string.IsNullOrWhiteSpace(currentRepoRoot)
            && IsRuntimeAuthorityRoot(currentRepoRoot)
            && !PathEquals(currentRepoRoot, targetRepoRoot))
        {
            return currentRepoRoot;
        }

        return IsRuntimeAuthorityRoot(targetRepoRoot) ? targetRepoRoot : null;
    }

    private static bool IsRuntimeAuthorityRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return File.Exists(Path.Combine(fullPath, "CARVES.Runtime.sln"))
               && File.Exists(Path.Combine(fullPath, "src", "CARVES.Runtime.Cli", "carves.csproj"));
    }
}
