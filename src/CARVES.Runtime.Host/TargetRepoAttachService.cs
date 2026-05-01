using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.ControlPlane;

namespace Carves.Runtime.Host;

public sealed class TargetRepoAttachService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly RuntimeServices services;

    public TargetRepoAttachService(RuntimeServices services)
    {
        this.services = services;
    }

    public Carves.Runtime.Application.ControlPlane.OperatorCommandResult Attach(
        string repoPath,
        string? repoId,
        string? providerProfile,
        string? policyProfile,
        bool startRuntime,
        bool dryRun,
        bool force = false,
        string? clientRepoRoot = null)
    {
        var targetRoot = ResolveAttachRoot(repoPath);
        var targetPaths = ControlPlanePaths.FromRepoRoot(targetRoot);
        var callerRepoRoot = string.IsNullOrWhiteSpace(clientRepoRoot)
            ? services.Paths.RepoRoot
            : Path.GetFullPath(clientRepoRoot);
        if (!Directory.Exists(targetRoot))
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure(
                $"Attach mode: invalid_target",
                $"Repo path '{targetRoot}' does not exist.");
        }

        if (!services.GitClient.IsRepository(targetRoot))
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure(
                "Attach mode: invalid_target",
                $"Repo path '{targetRoot}' is not a git repository.");
        }

        var uncommittedPaths = services.GitClient.GetUncommittedPaths(targetRoot);
        if (HasUserFacingRepoChanges(uncommittedPaths))
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure(
                "Attach mode: dirty_target",
                $"Repo path '{targetRoot}' has uncommitted changes. Attach expects a clean target.");
        }

        var attachMode = DetermineAttachMode(targetRoot, targetPaths);
        var currentHostDescriptor = TryLoadCurrentHostDescriptor();
        var hostSessionId = BuildHostSessionId(currentHostDescriptor);
        var hostSessionService = new HostSessionService(services.Paths);
        hostSessionService.Ensure(
            hostSessionId,
            currentHostDescriptor?.HostId ?? LocalHostPaths.GetHostId(services.Paths.RepoRoot),
            services.Paths.RepoRoot,
            currentHostDescriptor?.BaseUrl,
            RuntimeStageInfo.CurrentStage,
            currentHostDescriptor?.StartedAt ?? DateTimeOffset.UtcNow);
        var attachLockResult = AcquireAttachLock(targetPaths, hostSessionId, callerRepoRoot, force);
        if (!attachLockResult.Allowed)
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure(
                "Attach mode: conflict",
                attachLockResult.Message ?? "Attach lock rejected the request.");
        }

        var initializedFiles = InitializeTargetControlPlane(targetRoot);
        var descriptor = services.RepoRegistryService.Register(targetRoot, repoId, providerProfile, policyProfile);
        services.RepoRuntimeService.Upsert(targetRoot, Carves.Runtime.Domain.Platform.RepoRuntimeStatus.Idle);
        var targetServices = RuntimeComposition.Create(targetRoot);
        var manifestService = new RuntimeManifestService(targetPaths, new ControlPlaneLockService(targetPaths.RepoRoot));
        var healthCheckService = new RuntimeHealthCheckService(targetPaths, targetServices.TaskGraphService);
        var projectUnderstandingBeforeAttach = targetServices.ProjectUnderstandingProjectionService.Evaluate(hydrateIfNeeded: false);
        var opportunities = targetServices.OperatorSurfaceService.DetectOpportunities();
        var sync = targetServices.OperatorSurfaceService.SyncState();
        var readiness = BuildReadinessSummary(targetRoot, descriptor.RepoId);
        var attachRitual = targetServices.InteractionLayerService.BuildAttachSummary(
            descriptor.RepoId,
            descriptor.RepoPath,
            attachMode,
            readiness.ReadyState,
            readiness.Summary);
        attachRitual = NormalizeProjectUnderstandingAction(attachRitual, projectUnderstandingBeforeAttach);
        var branch = ResolveCurrentBranch(targetRoot);
        var health = healthCheckService.Evaluate();
        var runtimeVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var manifest = manifestService.Upsert(
            descriptor.RepoId,
            descriptor.RepoPath,
            targetRoot,
            services.Paths.RepoRoot,
            branch,
            runtimeVersion,
            runtimeVersion,
            hostSessionId,
            health.State.ToString().ToLowerInvariant(),
            readiness.Summary,
            health.State switch
            {
                RepoRuntimeHealthState.Healthy => RepoRuntimeManifestState.Healthy,
                RepoRuntimeHealthState.Dirty => RepoRuntimeManifestState.Dirty,
                _ => RepoRuntimeManifestState.Repairing,
            });
        hostSessionService.BindRepo(
            hostSessionId,
            descriptor.RepoId,
            descriptor.RepoPath,
            callerRepoRoot,
            attachMode,
            health.State.ToString().ToLowerInvariant());
        var attachHandshakeService = new AttachHandshakeService(targetPaths);
        attachHandshakeService.Persist(
            new AttachHandshakeRequestRecord
            {
                RepoPath = descriptor.RepoPath,
                GitRoot = targetRoot,
                Branch = branch,
                ClientVersion = runtimeVersion,
                RuntimeVersion = runtimeVersion,
                ClientRepoRoot = callerRepoRoot,
                RuntimeRoot = services.Paths.RepoRoot,
            },
            new AttachHandshakeAcknowledgement
            {
                RepoId = descriptor.RepoId,
                HostSessionId = hostSessionId,
                Status = "attached",
                AttachedAt = manifest.LastAttachedAt,
                RuntimeStatus = health.State.ToString().ToLowerInvariant(),
                RepoSummary = readiness.Summary,
                AttachMode = attachMode,
            });
        MirrorHostDescriptorToTarget(targetRoot, currentHostDescriptor);

        var lines = new List<string>
        {
            "CARVES runtime attached.",
            $"Attached repo {descriptor.RepoId}.",
            $"Attach mode: {attachMode}",
            $"Path: {descriptor.RepoPath}",
            $"Stage: {RuntimeStageReader.TryRead(targetRoot) ?? "(unknown)"}",
            $"Protocol mode: {attachRitual.ProtocolMode}",
            $"Conversation phase: {attachRitual.Protocol.CurrentPhase.ToString().ToLowerInvariant()}",
            $"Intent state: {attachRitual.Intent.State.ToString().ToLowerInvariant()}",
            $"Prompt kernel: {attachRitual.PromptKernel.KernelId}@{attachRitual.PromptKernel.Version}",
            $"Prompt template: {attachRitual.ActiveTemplate.TemplateId}@{attachRitual.ActiveTemplate.Version}",
            $"Project understanding: {attachRitual.ProjectUnderstanding.State.ToString().ToLowerInvariant()} ({attachRitual.ProjectUnderstanding.Action})",
            $"Project summary: {attachRitual.ProjectUnderstanding.Summary}",
            $"Next recommended action: {attachRitual.RecommendedNextAction}",
            $"Provider profile: {descriptor.ProviderProfile}",
            $"Policy profile: {descriptor.PolicyProfile}",
            $"Initialized files: {initializedFiles}",
            $"Readiness: {readiness.ReadyState}",
            $"Readiness summary: {readiness.Summary}",
            $"Attached at: {manifest.LastAttachedAt:O}",
            $"Host session id: {manifest.HostSessionId}",
            $"Runtime manifest: {manifestService.ManifestPath}",
            $"Attach handshake: {attachHandshakeService.HandshakePath}",
            $"Runtime health: {health.State.ToString().ToLowerInvariant()}",
            $"Runtime summary: {health.Summary}",
            $"Runtime suggested action: {health.SuggestedAction}",
        };
        lines.AddRange(opportunities.Lines);
        lines.AddRange(sync.Lines);

        if (startRuntime)
        {
            lines.AddRange(services.OperatorSurfaceService.RuntimeStart(descriptor.RepoId, dryRun).Lines);
        }
        else
        {
            lines.Add("Runtime start deferred. The repo is ready for operator start.");
        }

        return new Carves.Runtime.Application.ControlPlane.OperatorCommandResult(0, lines);
    }

    private static Carves.Runtime.Application.Interaction.AttachRitualSummary NormalizeProjectUnderstandingAction(
        Carves.Runtime.Application.Interaction.AttachRitualSummary summary,
        Carves.Runtime.Application.Interaction.ProjectUnderstandingProjection previousProjection)
    {
        if (summary.ProjectUnderstanding.State != Carves.Runtime.Application.Interaction.ProjectUnderstandingState.Fresh)
        {
            return summary;
        }

        if (previousProjection.State is Carves.Runtime.Application.Interaction.ProjectUnderstandingState.Missing
            or Carves.Runtime.Application.Interaction.ProjectUnderstandingState.Stale
            or Carves.Runtime.Application.Interaction.ProjectUnderstandingState.Deferred)
        {
            return summary with
            {
                ProjectUnderstanding = summary.ProjectUnderstanding with
                {
                    Action = "refreshed",
                    Rationale = "attach refreshed the project understanding projection",
                },
            };
        }

        return summary with
        {
            ProjectUnderstanding = summary.ProjectUnderstanding with
            {
                Action = "reused",
                Rationale = "attach reused the existing fresh project understanding projection",
            },
        };
    }

    private static bool HasUserFacingRepoChanges(IReadOnlyList<string> uncommittedPaths)
    {
        if (uncommittedPaths.Count == 0)
        {
            return false;
        }

        return uncommittedPaths.Any(path => !IsControlPlanePath(path));
    }

    private static bool IsControlPlanePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/').Trim();
        return string.Equals(normalized, ".ai", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, ".carves-platform", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".carves-platform/", StringComparison.OrdinalIgnoreCase);
    }

    private string DetermineAttachMode(string targetRoot, ControlPlanePaths targetPaths)
    {
        var isRegistered = services.RepoRegistryService.List().Any(descriptor =>
            string.Equals(Path.GetFullPath(descriptor.RepoPath), targetRoot, StringComparison.OrdinalIgnoreCase));
        var hasSystemConfig = File.Exists(targetPaths.SystemConfigFile);
        var hasAnyAiRoot = Directory.Exists(targetPaths.AiRoot);
        if (isRegistered || hasSystemConfig)
        {
            return "re_attach";
        }

        if (hasAnyAiRoot)
        {
            return "partial_init";
        }

        return "fresh_init";
    }

    private int InitializeTargetControlPlane(string targetRoot)
    {
        var targetPaths = ControlPlanePaths.FromRepoRoot(targetRoot);
        Directory.CreateDirectory(targetPaths.ConfigRoot);
        Directory.CreateDirectory(Path.Combine(targetPaths.AiRoot, "memory"));
        Directory.CreateDirectory(targetPaths.TasksRoot);
        Directory.CreateDirectory(targetPaths.TaskNodesRoot);
        Directory.CreateDirectory(targetPaths.CardsRoot);
        Directory.CreateDirectory(targetPaths.RuntimeRoot);
        Directory.CreateDirectory(Path.Combine(targetPaths.AiRoot, "codegraph"));
        Directory.CreateDirectory(Path.Combine(targetPaths.AiRoot, "patches"));
        Directory.CreateDirectory(Path.Combine(targetPaths.AiRoot, "reviews"));
        Directory.CreateDirectory(targetPaths.OpportunitiesRoot);
        Directory.CreateDirectory(targetPaths.ArtifactsRoot);
        Directory.CreateDirectory(targetPaths.WorkerArtifactsRoot);
        Directory.CreateDirectory(targetPaths.SafetyArtifactsRoot);
        Directory.CreateDirectory(targetPaths.ReviewArtifactsRoot);
        Directory.CreateDirectory(targetPaths.ProviderArtifactsRoot);
        Directory.CreateDirectory(targetPaths.MergeArtifactsRoot);
        Directory.CreateDirectory(targetPaths.RuntimeFailureArtifactsRoot);

        var initialized = 0;
        initialized += EnsureSystemConfig(targetRoot, targetPaths);
        initialized += EnsureTextFile(Path.Combine(targetPaths.AiRoot, "STATE.md"), "# STATE\n\nStage: attached\n");
        initialized += EnsureTextFile(Path.Combine(targetPaths.AiRoot, "PROJECT_BOUNDARY.md"), "# PROJECT BOUNDARY\n\nDescribe the governed scope here.\n");
        initialized += EnsureTextFile(Path.Combine(targetPaths.AiRoot, "AGENT_BOOTSTRAP.md"), BuildTargetAgentBootstrapContent(targetRoot));
        initialized += EnsureTextFile(Path.Combine(targetRoot, "AGENTS.md"), BuildRootAgentsContent(targetRoot));
        initialized += EnsureLauncherFile(
            Path.Combine(targetRoot, RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath.Replace('/', Path.DirectorySeparatorChar)),
            RuntimeTargetAgentBootstrapPackService.BuildProjectLocalLauncherContent(services.Paths.RepoRoot));
        initialized += EnsureTextFile(
            Path.Combine(targetRoot, RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath.Replace('/', Path.DirectorySeparatorChar)),
            RuntimeTargetAgentBootstrapPackService.BuildAgentStartMarkdownContent(services.Paths.RepoRoot, targetRoot));
        initialized += EnsureTextFile(
            Path.Combine(targetRoot, RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath.Replace('/', Path.DirectorySeparatorChar)),
            RuntimeTargetAgentBootstrapPackService.BuildAgentStartJsonContent(services.Paths.RepoRoot, targetRoot));
        initialized += EnsureCopiedConfig(services.Paths.AiProviderConfigFile, targetPaths.AiProviderConfigFile);
        initialized += EnsureCopiedConfig(services.Paths.PlannerAutonomyConfigFile, targetPaths.PlannerAutonomyConfigFile);
        initialized += EnsureCopiedConfig(services.Paths.CarvesCodeStandardFile, targetPaths.CarvesCodeStandardFile);
        initialized += EnsureCopiedConfig(services.Paths.SafetyRulesFile, targetPaths.SafetyRulesFile);
        initialized += EnsureCopiedConfig(services.Paths.ModuleDependenciesFile, targetPaths.ModuleDependenciesFile);

        return initialized;
    }

    private string BuildTargetAgentBootstrapContent(string targetRoot)
    {
        var repoName = Path.GetFileName(targetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var runtimeRoot = Path.GetFullPath(services.Paths.RepoRoot);
        var carvesCommand = RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath;
        var shellLanguage = RuntimeCliWrapperPaths.ShellBlockLanguage;
        return string.Join(
            Environment.NewLine,
            [
                "# CARVES Agent Bootstrap",
                string.Empty,
                $"Repository: `{repoName}`",
                $"Runtime root: `{runtimeRoot}`",
                $"Project-local launcher: `{RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath}`",
                $"Agent start packet: `{RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath}` and `{RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath}`",
                string.Empty,
                "This repository is attached to CARVES Runtime. Treat CARVES as the planning, governance, workspace, review, and writeback authority for durable work.",
                string.Empty,
                "## First Command",
                string.Empty,
                $"Before planning or editing, read `{RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath}` and run:",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Read `current_stage_id`, `next_governed_command`, `next_command_source`, `available_actions`, `minimal_agent_rules`, and `gaps`. Treat `next_governed_command` as a legacy projection hint, prefer `available_actions` when present, and use the detailed readbacks below only when this single start payload reports a gap or the operator asks for deeper evidence.",
                string.Empty,
                "## Required Rules",
                string.Empty,
                "- Do not run `carves init` again when `.ai/runtime.json` already exists, unless `pilot status` explicitly reports `attach_target`.",
                "- Do not edit `.ai/` official truth manually. Use CARVES commands for intent, planning, task, review, and writeback truth.",
                "- Do not copy Runtime docs into this target repo. Read Runtime-owned docs from the Runtime root shown above or via CARVES surfaces.",
                "- Do not claim Runtime alpha external-use readiness unless `carves pilot readiness --json` reports `alpha_external_use_ready=true`.",
                "- Do not assume a global `carves` alias is authoritative until `carves pilot invocation --json` confirms the intended Runtime root.",
                "- Do not edit shell profiles, machine PATH, or global tool installation as project work; `carves pilot activation --json` is read-only operator guidance.",
                "- Do not claim frozen dist freshness until `carves pilot dist-smoke --json` reports `local_dist_freshness_smoke_ready=true`.",
                "- Do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` to retarget Runtime manually; `carves pilot dist-binding --json` is read-only operator guidance.",
                "- Do not claim external target frozen-dist readiness until `carves pilot target-proof --json` reports `frozen_dist_target_readback_proof_complete=true`.",
                "- Do not commit `.ai/runtime/live-state/` by default.",
                "- Prefer a frozen local Runtime dist over the live Runtime source tree for stable external-project work; confirm with `carves pilot dist --json`.",
                "- Do not directly copy files from a managed workspace into the target repo. Use `carves plan submit-workspace` and `carves review approve`.",
                "- If a CARVES stop trigger fires, run `carves pilot problem-intake --json`, submit `carves pilot report-problem <json-path> --json`, run `carves pilot triage --json`, run `carves pilot follow-up --json`, run `carves pilot follow-up-plan --json`, run `carves pilot follow-up-record --json`, run `carves pilot follow-up-intake --json`, run `carves pilot follow-up-gate --json`, then stop instead of rationalizing the warning.",
                "- If `carves pilot follow-up-record --json` reports missing follow-up decisions, ask the operator to run `carves pilot record-follow-up-decision <decision> --all --reason <reason>` before turning an accepted pattern into governed planning input.",
                "- If `carves pilot follow-up-intake --json` reports accepted planning items, run `carves pilot follow-up-gate --json` and convert them only through its next governed command; do not edit card or task truth directly.",
                "- Before staging or committing target changes, run `carves pilot commit-plan` and follow its stage/exclude/review path lists.",
                "- After committing target changes, run `carves pilot closure --json` and confirm `commit_closure_complete=true`.",
                "- If `target_git_worktree_clean=false` only because excluded local/tooling residue remains, run `carves pilot residue --json` before deciding whether to keep it local or add reviewed ignore entries.",
                "- Before editing `.gitignore` for CARVES residue, run `carves pilot ignore-plan --json` and treat its patch preview as an operator-review candidate only.",
                "- If `carves pilot ignore-plan --json` reports missing ignore entries, run `carves pilot ignore-record --json` and record the operator decision with `carves pilot record-ignore-decision <decision> --all --reason <reason>` before final proof.",
                "- Use `carves pilot proof --json` as the final read-only aggregate before declaring an external-project pilot closed.",
                "- Before formal planning, use CARVES intent and plan commands; do not create multiple competing planning cards.",
                "- When `pilot status` reports a blocked stage, resolve the named gap before editing project files.",
                string.Empty,
                "## Typical Readbacks",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "start", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "readiness", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "invocation", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "activation", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "dist-smoke", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "dist-binding", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "target-proof", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "status", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "resources", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "problem-intake", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "triage", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "follow-up", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "follow-up-plan", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "follow-up-record", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "follow-up-intake", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "follow-up-gate", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "dist", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "handoff", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "guide"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "commit-plan"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "closure", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "residue", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "ignore-plan", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "ignore-record", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "pilot", "proof", "--json"),
                "```",
                string.Empty,
                "## Non-Claims",
                string.Empty,
                "This bootstrap is a portable prompt-level and repo-level instruction layer. It does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, or automatic git commit/push.",
                string.Empty,
            ]);
    }

    private string BuildRootAgentsContent(string targetRoot)
    {
        var repoName = Path.GetFileName(targetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var carvesCommand = RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath;
        var shellLanguage = RuntimeCliWrapperPaths.ShellBlockLanguage;
        return string.Join(
            Environment.NewLine,
            [
                "# AGENTS",
                string.Empty,
                $"This repository (`{repoName}`) is attached to CARVES Runtime.",
                string.Empty,
                $"Before planning, analysis, or edits, read `{RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath}` and `.ai/AGENT_BOOTSTRAP.md`, then run:",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Treat `next_governed_command` from CARVES as a legacy projection hint. Prefer `available_actions` when present, and do not bypass CARVES official truth, managed workspace, review, or writeback gates.",
                string.Empty,
            ]);
    }

    private AttachReadiness BuildReadinessSummary(string targetRoot, string repoId)
    {
        var paths = ControlPlanePaths.FromRepoRoot(targetRoot);
        var items = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["system_config"] = File.Exists(paths.SystemConfigFile),
            ["codegraph_index"] = File.Exists(Path.Combine(targetRoot, ".ai", "codegraph", "index.json")),
            ["opportunity_index"] = File.Exists(paths.OpportunitiesFile),
            ["task_graph"] = File.Exists(paths.TaskGraphFile),
            ["platform_registration"] = services.RepoRegistryService.List().Any(descriptor => string.Equals(descriptor.RepoId, repoId, StringComparison.Ordinal)),
        };

        var missing = items.Where(pair => !pair.Value).Select(pair => pair.Key).ToArray();
        return new AttachReadiness(
            missing.Length == 0 ? "ready" : "partial",
            missing.Length == 0
                ? "repo is inspectable by host and ready for runtime/planner references"
                : $"missing: {string.Join(", ", missing)}");
    }

    private int EnsureSystemConfig(string targetRoot, ControlPlanePaths targetPaths)
    {
        if (File.Exists(targetPaths.SystemConfigFile))
        {
            return 0;
        }

        var repoName = Path.GetFileName(targetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var defaultConfig = SystemConfig.CreateDefault(repoName);
        var defaultTestCommand = ResolveDefaultTestCommand(targetRoot);
        var codeDirectories = CodeDirectoryDiscoveryPolicy.Discover(targetRoot, defaultConfig);
        var config = new
        {
            repo_name = repoName,
            worktree_root = $"../.carves-worktrees/{repoName}",
            max_parallel_tasks = services.SystemConfig.MaxParallelTasks,
            default_test_command = defaultTestCommand,
            code_directories = codeDirectories.Count == 0 ? defaultConfig.CodeDirectories : codeDirectories,
            excluded_directories = services.SystemConfig.ExcludedDirectories,
            sync_markdown_views = services.SystemConfig.SyncMarkdownViews,
            remove_worktree_on_success = services.SystemConfig.RemoveWorktreeOnSuccess,
        };

        File.WriteAllText(targetPaths.SystemConfigFile, JsonSerializer.Serialize(config, JsonOptions));
        return 1;
    }

    private int EnsureCopiedConfig(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath) || !File.Exists(sourcePath))
        {
            return 0;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
        return 1;
    }

    private static IReadOnlyList<string> ResolveDefaultTestCommand(string targetRoot)
    {
        var solution = Directory.EnumerateFiles(targetRoot, "*.sln", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (solution is not null)
        {
            return ["dotnet", "test", Path.GetFileName(solution)];
        }

        var project = Directory.EnumerateFiles(targetRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (project is not null)
        {
            var relative = Path.GetRelativePath(targetRoot, project).Replace(Path.DirectorySeparatorChar, '/');
            return ["dotnet", "test", relative];
        }

        return ["dotnet", "test"];
    }

    private string ResolveAttachRoot(string repoPath)
    {
        var candidate = string.IsNullOrWhiteSpace(repoPath) ? services.Paths.RepoRoot : Path.GetFullPath(repoPath);
        if (services.GitClient.IsRepository(candidate))
        {
            return candidate;
        }

        var current = new DirectoryInfo(candidate);
        while (current is not null)
        {
            if (services.GitClient.IsRepository(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(repoPath);
    }

    private static int EnsureTextFile(string path, string content)
    {
        if (File.Exists(path))
        {
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return 1;
    }

    private static int EnsureLauncherFile(string path, string content)
    {
        var created = EnsureTextFile(path, content);
        if (created == 1)
        {
            TrySetUnixExecutable(path);
        }

        return created;
    }

    private static void TrySetUnixExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Filesystem mode support is best-effort for target repos.
        }
    }

    private LocalHostDescriptor? TryLoadCurrentHostDescriptor()
    {
        var descriptorPath = LocalHostPaths.GetDescriptorPath(services.Paths.RepoRoot);
        if (!File.Exists(descriptorPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LocalHostDescriptor>(File.ReadAllText(descriptorPath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildHostSessionId(LocalHostDescriptor? descriptor)
    {
        return descriptor is null
            ? $"cold-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
            : HostSessionService.BuildSessionId(descriptor.HostId, descriptor.StartedAt);
    }

    private static string ResolveCurrentBranch(string repoRoot)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.WorkingDirectory = repoRoot;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.ArgumentList.Add("branch");
            process.StartInfo.ArgumentList.Add("--show-current");
            process.Start();
            process.WaitForExit();
            var branch = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(branch) ? "(detached)" : branch;
        }
        catch
        {
            return "(unknown)";
        }
    }

    private static void MirrorHostDescriptorToTarget(string targetRoot, LocalHostDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        var mirrored = descriptor with
        {
            RepoRoot = targetRoot,
        };
        var path = LocalHostPaths.GetDescriptorPath(targetRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(mirrored, JsonOptions));
    }

    private static AttachLockDecision AcquireAttachLock(ControlPlanePaths paths, string hostSessionId, string clientRepoRoot, bool force)
    {
        var lockPath = Path.Combine(paths.RuntimeRoot, "attach.lock.json");
        if (!File.Exists(lockPath))
        {
            WriteAttachLock(lockPath, hostSessionId, clientRepoRoot);
            return new AttachLockDecision(true, null);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AttachLockPayload>(File.ReadAllText(lockPath), JsonOptions);
            if (payload is not null
                && string.Equals(Path.GetFullPath(payload.ClientRepoRoot), Path.GetFullPath(clientRepoRoot), StringComparison.OrdinalIgnoreCase))
            {
                WriteAttachLock(lockPath, hostSessionId, clientRepoRoot);
                return new AttachLockDecision(true, null);
            }

            if (!force)
            {
                return new AttachLockDecision(false, $"Repo attach is already controlled by client '{payload?.ClientRepoRoot ?? "(unknown)"}' in host session '{payload?.HostSessionId ?? "(unknown)"}'. Use --force to take over.");
            }
        }
        catch
        {
            if (!force)
            {
                return new AttachLockDecision(false, "Repo attach lock is unreadable. Use --force to take over.");
            }
        }

        WriteAttachLock(lockPath, hostSessionId, clientRepoRoot);
        return new AttachLockDecision(true, null);
    }

    private static void WriteAttachLock(string lockPath, string hostSessionId, string clientRepoRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllText(lockPath, JsonSerializer.Serialize(new AttachLockPayload(hostSessionId, Path.GetFullPath(clientRepoRoot), "active", DateTimeOffset.UtcNow), JsonOptions));
    }

    private sealed record AttachReadiness(string ReadyState, string Summary);
    private sealed record AttachLockPayload(string HostSessionId, string ClientRepoRoot, string ControlMode, DateTimeOffset AttachedAt);
    private sealed record AttachLockDecision(bool Allowed, string? Message);
}
