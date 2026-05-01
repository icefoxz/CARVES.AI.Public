using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetAgentBootstrapPackService
{
    public const string PhaseDocumentPath = RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md";
    public const string ProjectLocalLauncherPath = ".carves/carves";
    public const string AgentStartMarkdownPath = ".carves/AGENT_START.md";
    public const string AgentStartJsonPath = ".carves/agent-start.json";
    public const string VisibleAgentStartPath = "CARVES_START.md";
    private const string ProjectLocalGatewayStatusCommand = ".carves/carves gateway status";
    private const string ProjectLocalStatusWatchCommand = ".carves/carves status --watch --iterations 1 --interval-ms 0";
    private const string ProjectLocalForegroundGatewayCommand = ".carves/carves gateway";
    private const string AgentStartCopyPastePrompt = """
start CARVES

Read CARVES_START.md first. Then run .carves/carves agent start --json from this project and follow CARVES output. Do not plan or edit before that readback.
""";

    private static readonly JsonSerializerOptions AgentStartJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetAgentBootstrapPackService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetAgentBootstrapPackSurface Build(bool writeRequested)
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.PreviousPhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidateRuntimeDocument(RuntimeExternalConsumerResourcePackService.PhaseDocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
        ValidateRuntimeDocument(RuntimeExternalConsumerResourcePackService.PreviousPhaseDocumentPath, "Product closure Phase 17 product pilot proof document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Target agent bootstrap pack guide document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);

        var runtimeInitialized = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var targetAgentBootstrapFullPath = Path.Combine(repoRoot, ".ai", "AGENT_BOOTSTRAP.md");
        var rootAgentsFullPath = Path.Combine(repoRoot, "AGENTS.md");
        var projectLocalLauncherFullPath = ToRepoPath(ProjectLocalLauncherPath);
        var agentStartMarkdownFullPath = ToRepoPath(AgentStartMarkdownPath);
        var agentStartJsonFullPath = ToRepoPath(AgentStartJsonPath);
        var visibleAgentStartFullPath = ToRepoPath(VisibleAgentStartPath);
        var targetAgentBootstrapExistsBefore = File.Exists(targetAgentBootstrapFullPath);
        var rootAgentsExistsBefore = File.Exists(rootAgentsFullPath);
        var projectLocalLauncherExistsBefore = File.Exists(projectLocalLauncherFullPath);
        var agentStartMarkdownExistsBefore = File.Exists(agentStartMarkdownFullPath);
        var agentStartJsonExistsBefore = File.Exists(agentStartJsonFullPath);
        var visibleAgentStartExistsBefore = File.Exists(visibleAgentStartFullPath);
        var materialized = new List<string>();
        var skipped = new List<string>();

        if (writeRequested && runtimeInitialized)
        {
            EnsureTextFile(targetAgentBootstrapFullPath, BuildTargetAgentBootstrapContent(), materialized, skipped, ".ai/AGENT_BOOTSTRAP.md");
            EnsureTextFile(rootAgentsFullPath, BuildRootAgentsContent(), materialized, skipped, "AGENTS.md");
            EnsureLauncherFile(
                projectLocalLauncherFullPath,
                BuildProjectLocalLauncherContent(documentRoot.DocumentRoot),
                materialized,
                skipped,
                ProjectLocalLauncherPath);
            EnsureGeneratedProjectionFile(
                agentStartMarkdownFullPath,
                BuildAgentStartMarkdownContent(documentRoot.DocumentRoot, repoRoot),
                materialized,
                skipped,
                AgentStartMarkdownPath,
                IsGeneratedAgentStartMarkdown);
            EnsureGeneratedProjectionFile(
                agentStartJsonFullPath,
                BuildAgentStartJsonContent(documentRoot.DocumentRoot, repoRoot),
                materialized,
                skipped,
                AgentStartJsonPath,
                IsGeneratedAgentStartJson);
            EnsureGeneratedProjectionFile(
                visibleAgentStartFullPath,
                BuildVisibleAgentStartContent(documentRoot.DocumentRoot, repoRoot),
                materialized,
                skipped,
                VisibleAgentStartPath,
                IsGeneratedVisibleAgentStart);
        }
        else if (writeRequested)
        {
            errors.Add("Target runtime is not initialized; run `carves init [target-path] --json` before materializing the agent bootstrap pack.");
        }

        var targetAgentBootstrapExists = File.Exists(targetAgentBootstrapFullPath);
        var rootAgentsExists = File.Exists(rootAgentsFullPath);
        var rootAgentsContainsCarvesEntry = RootAgentsContainsCarvesEntry(rootAgentsFullPath);
        var rootAgentsIntegrationPosture = ResolveRootAgentsIntegrationPosture(
            rootAgentsExistsBefore,
            rootAgentsExists,
            rootAgentsContainsCarvesEntry);
        var rootAgentsSuggestedPatch = rootAgentsContainsCarvesEntry
            ? string.Empty
            : BuildRootAgentsSuggestedPatch();
        var projectLocalLauncherExists = File.Exists(projectLocalLauncherFullPath);
        var agentStartMarkdownExists = File.Exists(agentStartMarkdownFullPath);
        var agentStartJsonExists = File.Exists(agentStartJsonFullPath);
        var visibleAgentStartExists = File.Exists(visibleAgentStartFullPath);
        var missing = new List<string>();
        if (!targetAgentBootstrapExists)
        {
            missing.Add(".ai/AGENT_BOOTSTRAP.md");
        }

        if (!rootAgentsExists)
        {
            missing.Add("AGENTS.md");
        }

        if (!projectLocalLauncherExists)
        {
            missing.Add(ProjectLocalLauncherPath);
        }

        if (!agentStartMarkdownExists)
        {
            missing.Add(AgentStartMarkdownPath);
        }

        if (!agentStartJsonExists)
        {
            missing.Add(AgentStartJsonPath);
        }

        if (!visibleAgentStartExists)
        {
            missing.Add(VisibleAgentStartPath);
        }

        var posture = ResolvePosture(runtimeInitialized, writeRequested, missing.Count, materialized.Count, errors.Count);
        return new RuntimeTargetAgentBootstrapPackSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = posture,
            RuntimeInitialized = runtimeInitialized,
            TargetAgentBootstrapExists = targetAgentBootstrapExists,
            RootAgentsExists = rootAgentsExists,
            RootAgentsContainsCarvesEntry = rootAgentsContainsCarvesEntry,
            RootAgentsIntegrationPosture = rootAgentsIntegrationPosture,
            RootAgentsSuggestedPatch = rootAgentsSuggestedPatch,
            ProjectLocalLauncherPath = ProjectLocalLauncherPath,
            ProjectLocalLauncherExists = projectLocalLauncherExists,
            AgentStartMarkdownPath = AgentStartMarkdownPath,
            AgentStartMarkdownExists = agentStartMarkdownExists,
            AgentStartJsonPath = AgentStartJsonPath,
            AgentStartJsonExists = agentStartJsonExists,
            VisibleAgentStartPath = VisibleAgentStartPath,
            VisibleAgentStartExists = visibleAgentStartExists,
            CanMaterialize = runtimeInitialized,
            WriteRequested = writeRequested,
            MissingFiles = missing,
            MaterializedFiles = materialized,
            SkippedFiles = skipped,
            Summary = BuildSummary(
                runtimeInitialized,
                writeRequested,
                targetAgentBootstrapExistsBefore,
                rootAgentsExistsBefore,
                projectLocalLauncherExistsBefore,
                agentStartMarkdownExistsBefore,
                agentStartJsonExistsBefore,
                visibleAgentStartExistsBefore,
                missing.Count,
                materialized.Count),
            RecommendedNextAction = BuildRecommendedNextAction(runtimeInitialized, missing.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not overwrite an existing target-owned AGENTS.md.",
                "If target-owned AGENTS.md does not point at CARVES, this surface reports a suggested patch instead of applying it.",
                "This surface does not edit existing .ai/ official truth beyond creating the missing bootstrap file.",
                "The project-local launcher is an invocation convenience; it is not task truth, lifecycle truth, or worker execution authority.",
                "The project-local agent-start packet is a bootstrap projection; it is not Host readiness proof or lifecycle truth.",
                "This surface does not initialize runtime truth, create plans, create tasks, approve review, write back work, or commit git changes.",
                "This surface does not claim OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    public bool IsMissingRequiredBootstrap()
    {
        if (!File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json")))
        {
            return false;
        }

        return !File.Exists(Path.Combine(repoRoot, ".ai", "AGENT_BOOTSTRAP.md"))
               || !File.Exists(Path.Combine(repoRoot, "AGENTS.md"))
               || !File.Exists(ToRepoPath(ProjectLocalLauncherPath))
               || !File.Exists(ToRepoPath(AgentStartMarkdownPath))
               || !File.Exists(ToRepoPath(AgentStartJsonPath))
               || !File.Exists(ToRepoPath(VisibleAgentStartPath));
    }

    private string BuildTargetAgentBootstrapContent()
    {
        var repoName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var runtimeRoot = Path.GetFullPath(documentRoot.DocumentRoot);
        var carvesCommand = ProjectLocalLauncherPath;
        var shellLanguage = RuntimeCliWrapperPaths.ShellBlockLanguage;
        return string.Join(
            Environment.NewLine,
            [
                "# CARVES Agent Bootstrap",
                string.Empty,
                $"Repository: `{repoName}`",
                $"Runtime root: `{runtimeRoot}`",
                $"Project-local launcher: `{ProjectLocalLauncherPath}`",
                $"Agent start packet: `{AgentStartMarkdownPath}` and `{AgentStartJsonPath}`",
                $"Visible start pointer: `{VisibleAgentStartPath}`",
                string.Empty,
                "This repository is attached to CARVES Runtime. Treat CARVES as the planning, governance, workspace, review, and writeback authority for durable work.",
                string.Empty,
                "## First Command",
                string.Empty,
                $"Before planning or editing, read `{AgentStartMarkdownPath}` and run:",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Read `startup_entry_source`, `startup_boundary_ready`, `startup_boundary_posture`, `startup_boundary_gaps`, `target_project_classification`, `target_classification_owner`, `target_startup_mode`, `target_runtime_binding_status`, `target_bound_runtime_root`, `agent_target_classification_allowed`, `agent_runtime_rebind_allowed`, `current_stage_id`, `next_governed_command`, `next_command_source`, `available_actions`, `minimal_agent_rules`, and `gaps`. Treat `next_governed_command` as a legacy projection hint, prefer `available_actions` when present, and use the detailed readbacks below only when this single start payload reports a gap or the operator asks for deeper evidence.",
                string.Empty,
                "## Required Rules",
                string.Empty,
                "- Do not run `carves init` again when `.ai/runtime.json` already exists, unless `pilot status` explicitly reports `attach_target`.",
                "- Do not edit `.ai/` official truth manually. Use CARVES commands for intent, planning, task, review, and writeback truth.",
                "- Do not copy Runtime docs into this target repo. Read Runtime-owned docs from the Runtime root shown above or via CARVES surfaces.",
                $"- Inside this target, prefer `{ProjectLocalLauncherPath}` from this packet. A global `carves` shim is only a locator/dispatcher and must not replace the bound project-local launcher.",
                $"- If the operator asks whether CARVES is visibly running, use `{ProjectLocalGatewayStatusCommand}` or `{ProjectLocalStatusWatchCommand}` as read-only visibility checks. Use `{ProjectLocalForegroundGatewayCommand}` only when the operator explicitly wants a foreground gateway terminal.",
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

    private string BuildRootAgentsContent()
    {
        var repoName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var carvesCommand = ProjectLocalLauncherPath;
        var shellLanguage = RuntimeCliWrapperPaths.ShellBlockLanguage;
        return string.Join(
            Environment.NewLine,
            [
                "# AGENTS",
                string.Empty,
                $"This repository (`{repoName}`) is attached to CARVES Runtime.",
                string.Empty,
                $"Before planning, analysis, or edits, read `{AgentStartMarkdownPath}` and `.ai/AGENT_BOOTSTRAP.md`, then run:",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(carvesCommand, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Treat `next_governed_command` from CARVES as a legacy projection hint. Prefer `available_actions` when present, and do not bypass CARVES official truth, managed workspace, review, or writeback gates.",
                string.Empty,
            ]);
    }

    public static string BuildProjectLocalLauncherContent(string runtimeRoot)
    {
        var runtimeWrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(Path.GetFullPath(runtimeRoot));
        if (OperatingSystem.IsWindows())
        {
            return string.Join(
                Environment.NewLine,
                [
                    "# Generated by CARVES. Project-local launcher only; not a truth owner.",
                    $"$RuntimeWrapper = '{EscapePowerShellSingleQuoted(runtimeWrapper)}'",
                    "& $RuntimeWrapper @args",
                    "exit $LASTEXITCODE",
                    string.Empty,
                ]);
        }

        return string.Join(
            Environment.NewLine,
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                "# Generated by CARVES. Project-local launcher only; not a truth owner.",
                $"runtime_wrapper={BashSingleQuote(runtimeWrapper)}",
                "exec \"$runtime_wrapper\" \"$@\"",
                string.Empty,
            ]);
    }

    public static string BuildAgentStartMarkdownContent(string runtimeRoot, string targetRepoRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var normalizedTargetRepoRoot = Path.GetFullPath(targetRepoRoot);
        var runtimeWrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(normalizedRuntimeRoot);
        var shellLanguage = RuntimeCliWrapperPaths.ShellBlockLanguage;
        return string.Join(
            Environment.NewLine,
            [
                "# CARVES Agent Start",
                string.Empty,
                $"Runtime root: `{normalizedRuntimeRoot}`",
                $"Runtime wrapper: `{runtimeWrapper}`",
                $"Target repo root: `{normalizedTargetRepoRoot}`",
                $"Project-local launcher: `{ProjectLocalLauncherPath}`",
                $"Machine packet: `{AgentStartJsonPath}`",
                $"Visible start pointer: `{VisibleAgentStartPath}`",
                string.Empty,
                "If the operator says `start CARVES`, this is the intended action: read this file, then run the project-local command below.",
                string.Empty,
                "## Copy/Paste Prompt",
                string.Empty,
                "If the operator wants the shortest reliable handoff, paste this into the coding agent opened at the target project root:",
                string.Empty,
                "```text",
                AgentStartCopyPastePrompt,
                "```",
                string.Empty,
                "Run this before planning or editing:",
                string.Empty,
                $"```{shellLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(ProjectLocalLauncherPath, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Then follow `available_actions` first, or `recommended_next_action` / `next_governed_command` when CARVES only exposes the legacy projection.",
                "Read `startup_boundary_ready`, `startup_boundary_posture`, `startup_boundary_gaps`, `target_project_classification`, `target_classification_owner`, `target_startup_mode`, `target_runtime_binding_status`, `target_bound_runtime_root`, `agent_target_classification_allowed`, and `agent_runtime_rebind_allowed` from that readback. Do not use your own project classification or binding repair.",
                "If `startup_boundary_ready=false` or `thread_start_ready=false`, stop and show CARVES output to the operator before planning or editing.",
                string.Empty,
                "Stop and report instead of improvising when CARVES reports `blocked`, `gap`, missing bootstrap files, protected-root risk, or a command failure.",
                string.Empty,
                "## Is CARVES Running?",
                string.Empty,
                "If the operator wants visible proof that CARVES is reachable from this project, run one of these project-local read-only checks:",
                string.Empty,
                $"```{shellLanguage}",
                ProjectLocalGatewayStatusCommand,
                ProjectLocalStatusWatchCommand,
                "```",
                string.Empty,
                $"Use `{ProjectLocalForegroundGatewayCommand}` only when the operator explicitly wants a foreground gateway terminal. Do not use a global `carves` shim as authority inside this target; the bound project-local launcher wins.",
                string.Empty,
                "Do not classify this project as new or old yourself. This packet means `carves up` has already handled startup classification; use the machine packet and `.carves/carves agent start --json` readback instead of rerunning init or repairing bindings by hand.",
                "If CARVES reports `rebind_required`, `runtime_binding_mismatch`, or `operator_rebind_required`, stop and show the output to the operator. Do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` by hand.",
                string.Empty,
                "Current boundary: `.carves/carves` is only an invocation launcher. It does not dispatch worker automation, authorize API/SDK worker execution, approve review, sync state, or write lifecycle truth. The current worker execution boundary remains `null_worker` until a later governed product decision changes it.",
                string.Empty,
            ]);
    }

    public static string BuildAgentStartJsonContent(string runtimeRoot, string targetRepoRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var normalizedTargetRepoRoot = Path.GetFullPath(targetRepoRoot);
        var runtimeWrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(normalizedRuntimeRoot);
        var packet = new
        {
            schema_version = "carves.agent_start.v1",
            runtime_root = normalizedRuntimeRoot,
            runtime_wrapper = runtimeWrapper,
            target_repo_root = normalizedTargetRepoRoot,
            project_local_launcher = ProjectLocalLauncherPath,
            visible_start_file = VisibleAgentStartPath,
            human_start_prompt = "start CARVES",
            copy_paste_prompt = AgentStartCopyPastePrompt,
            agent_instruction = $"read {AgentStartMarkdownPath}, then run {ProjectLocalLauncherPath} agent start --json",
            target_classification_owner = "carves_up",
            agent_target_classification_allowed = false,
            agent_runtime_rebind_allowed = false,
            existing_project_rule = "If this packet exists, do not treat the target as a new project. Run first_agent_command and follow CARVES readback instead of rerunning init or repairing bindings by hand.",
            runtime_binding_rule = "If CARVES reports rebind_required or runtime_binding_mismatch, stop and show the output to the operator. Do not edit .ai/runtime.json or .ai/runtime/attach-handshake.json by hand.",
            agent_start_readback_fields = new[]
            {
                "startup_entry_source",
                "startup_boundary_ready",
                "startup_boundary_posture",
                "startup_boundary_gaps",
                "target_project_classification",
                "target_classification_owner",
                "target_startup_mode",
                "target_runtime_binding_status",
                "target_bound_runtime_root",
                "agent_target_classification_allowed",
                "agent_runtime_rebind_allowed",
                "worker_execution_boundary",
            },
            host_required = true,
            host_readiness = "not_checked_at_materialization",
            host_readiness_command = $"{ProjectLocalLauncherPath} host ensure --json",
            visible_gateway_commands = new[]
            {
                ProjectLocalGatewayStatusCommand,
                ProjectLocalStatusWatchCommand,
            },
            foreground_gateway_command = ProjectLocalForegroundGatewayCommand,
            global_shim_rule = "Inside a target project, prefer the bound project-local .carves/carves launcher. A global carves shim is only a locator/dispatcher and must not replace this packet, target binding, lifecycle truth, or worker execution boundary.",
            gateway_boundary = "resident_connection_routing_observability_only_no_worker_automation_dispatch",
            worker_execution_boundary = "null_worker_current_version_no_api_sdk_worker_execution",
            first_agent_command = $"{ProjectLocalLauncherPath} agent start --json",
            next_readback_command = $"{ProjectLocalLauncherPath} agent start --json",
            stop_triggers = new[]
            {
                "carves_reports_blocked",
                "carves_reports_gap",
                "command_failure",
                "protected_truth_root_direct_edit_required",
                "missing_acceptance_contract",
                "agent_tempted_to_bypass_carves_warning",
            },
            non_authority = new[]
            {
                "not_host_readiness_proof",
                "not_worker_execution_authority",
                "not_task_completion_truth",
                "not_review_approval",
                "not_state_sync",
                "not_merge_or_release_approval",
            },
            created_at_utc = DateTimeOffset.UtcNow,
        };

        return JsonSerializer.Serialize(packet, AgentStartJsonOptions) + Environment.NewLine;
    }

    public static string BuildRootAgentsSuggestedPatch()
    {
        return string.Join(
            Environment.NewLine,
            [
                "## CARVES",
                string.Empty,
                $"Before planning or editing CARVES-governed work, read `{AgentStartMarkdownPath}` and run:",
                string.Empty,
                $"```{RuntimeCliWrapperPaths.ShellBlockLanguage}",
                RuntimeCliWrapperPaths.FormatShellCommand(ProjectLocalLauncherPath, "agent", "start", "--json"),
                "```",
                string.Empty,
                "Follow `available_actions` first, or `recommended_next_action` / `next_governed_command` when CARVES only exposes the legacy projection. Stop and report if CARVES returns a blocker, a gap, a protected-root risk, or a command failure.",
                string.Empty,
                $"If the operator asks whether CARVES is running, use `{ProjectLocalGatewayStatusCommand}` or `{ProjectLocalStatusWatchCommand}`. Use `{ProjectLocalForegroundGatewayCommand}` only for an explicitly requested foreground gateway terminal.",
                string.Empty,
                $"Inside this target, prefer `{ProjectLocalLauncherPath}`. A global `carves` shim is only a locator/dispatcher and must not replace the bound project-local launcher.",
                string.Empty,
                $"`{ProjectLocalLauncherPath}` and `{AgentStartMarkdownPath}` are bootstrap projections only; they do not replace target-owned instructions, prove Host readiness, authorize worker execution, approve review, sync state, or write lifecycle truth.",
                string.Empty,
            ]);
    }

    public static string BuildVisibleAgentStartContent(string runtimeRoot, string targetRepoRoot)
    {
        var normalizedRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var normalizedTargetRepoRoot = Path.GetFullPath(targetRepoRoot);
        return string.Join(
            Environment.NewLine,
            [
                "# Start CARVES",
                string.Empty,
                $"Target repo root: `{normalizedTargetRepoRoot}`",
                $"Runtime root: `{normalizedRuntimeRoot}`",
                string.Empty,
                "If the operator says `start CARVES`, read the project-local CARVES start packet:",
                string.Empty,
                "## Copy/Paste Prompt",
                string.Empty,
                "Paste this into the coding agent opened at this project root:",
                string.Empty,
                "```text",
                AgentStartCopyPastePrompt,
                "```",
                string.Empty,
                "## What The Agent Should Do",
                string.Empty,
                $"1. Read `{AgentStartMarkdownPath}`.",
                $"2. Run `{ProjectLocalLauncherPath} agent start --json`.",
                "3. Follow `available_actions` first, and stop if CARVES reports a blocker, gap, protected-root risk, or command failure.",
                "4. Do not classify this project as new or old yourself; `carves up` already owns startup classification.",
                "5. Do not repair Runtime binding by hand; if CARVES reports `rebind_required`, show the output to the operator and stop.",
                $"6. If the operator asks whether CARVES is running, use `{ProjectLocalGatewayStatusCommand}` or `{ProjectLocalStatusWatchCommand}`. Use `{ProjectLocalForegroundGatewayCommand}` only for an explicitly requested foreground gateway terminal.",
                "7. Do not use a global `carves` shim as authority inside this target. The project-local `.carves/carves` launcher is the selected Runtime entry.",
                string.Empty,
                "This file is a visible pointer for coding agents. It is not Host readiness proof, worker execution authority, review approval, state sync, merge approval, or lifecycle truth.",
                string.Empty,
            ]);
    }

    private static bool RootAgentsContainsCarvesEntry(string rootAgentsFullPath)
    {
        if (!File.Exists(rootAgentsFullPath))
        {
            return false;
        }

        var content = File.ReadAllText(rootAgentsFullPath);
        return content.Contains(AgentStartMarkdownPath, StringComparison.OrdinalIgnoreCase)
               || content.Contains(ProjectLocalLauncherPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRootAgentsIntegrationPosture(
        bool rootAgentsExistsBefore,
        bool rootAgentsExists,
        bool rootAgentsContainsCarvesEntry)
    {
        if (!rootAgentsExists)
        {
            return "root_agents_missing_can_materialize_minimal_carves_entry";
        }

        if (!rootAgentsExistsBefore && rootAgentsContainsCarvesEntry)
        {
            return "root_agents_generated_minimal_carves_entry";
        }

        return rootAgentsContainsCarvesEntry
            ? "root_agents_carves_entry_present"
            : "target_owned_root_agents_preserved_manual_carves_entry_recommended";
    }

    private static string ResolvePosture(bool runtimeInitialized, bool writeRequested, int missingCount, int materializedCount, int errorCount)
    {
        if (errorCount > 0)
        {
            return runtimeInitialized
                ? "target_agent_bootstrap_blocked_by_surface_gaps"
                : "target_agent_bootstrap_blocked_by_runtime_init";
        }

        if (!runtimeInitialized)
        {
            return "target_agent_bootstrap_blocked_by_runtime_init";
        }

        if (missingCount == 0 && writeRequested && materializedCount > 0)
        {
            return "target_agent_bootstrap_materialized";
        }

        return missingCount == 0
            ? "target_agent_bootstrap_ready"
            : "target_agent_bootstrap_materialization_required";
    }

    private static string BuildSummary(
        bool runtimeInitialized,
        bool writeRequested,
        bool targetAgentBootstrapExistsBefore,
        bool rootAgentsExistsBefore,
        bool projectLocalLauncherExistsBefore,
        bool agentStartMarkdownExistsBefore,
        bool agentStartJsonExistsBefore,
        bool visibleAgentStartExistsBefore,
        int missingCount,
        int materializedCount)
    {
        if (!runtimeInitialized)
        {
            return "The target repo is not initialized; bootstrap repair waits for `.ai/runtime.json`.";
        }

        if (missingCount == 0 && writeRequested && materializedCount > 0)
        {
            return "Missing target agent bootstrap files were materialized without overwriting target-owned instructions.";
        }

        if (missingCount == 0)
        {
            return "Target agent bootstrap files are present.";
        }

        if (!targetAgentBootstrapExistsBefore
            && !rootAgentsExistsBefore
            && !projectLocalLauncherExistsBefore
            && !agentStartMarkdownExistsBefore
            && !agentStartJsonExistsBefore
            && !visibleAgentStartExistsBefore)
        {
            return "Target agent bootstrap entry files are missing and can be materialized safely.";
        }

        return "Target agent bootstrap entry is partially present; missing files can be materialized without overwriting existing files.";
    }

    private static string BuildRecommendedNextAction(bool runtimeInitialized, int missingCount)
    {
        if (!runtimeInitialized)
        {
            return "carves init [target-path] --json";
        }

        return missingCount == 0
            ? "carves pilot status --json"
            : "carves agent bootstrap --write";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private string ToRepoPath(string repoRelativePath)
    {
        return Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void EnsureTextFile(string path, string content, List<string> materialized, List<string> skipped, string repoRelativePath)
    {
        if (File.Exists(path))
        {
            skipped.Add(repoRelativePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        materialized.Add(repoRelativePath);
    }

    private static void EnsureGeneratedProjectionFile(
        string path,
        string content,
        List<string> materialized,
        List<string> skipped,
        string repoRelativePath,
        Func<string, bool> isGeneratedProjection)
    {
        if (!File.Exists(path))
        {
            EnsureTextFile(path, content, materialized, skipped, repoRelativePath);
            return;
        }

        var existing = File.ReadAllText(path);
        if (string.Equals(existing, content, StringComparison.Ordinal))
        {
            skipped.Add(repoRelativePath);
            return;
        }

        if (!isGeneratedProjection(existing))
        {
            skipped.Add(repoRelativePath);
            return;
        }

        File.WriteAllText(path, content);
        materialized.Add(repoRelativePath);
    }

    private static void EnsureLauncherFile(string path, string content, List<string> materialized, List<string> skipped, string repoRelativePath)
    {
        EnsureTextFile(path, content, materialized, skipped, repoRelativePath);
        if (materialized.Contains(repoRelativePath, StringComparer.Ordinal))
        {
            TrySetUnixExecutable(path);
        }
    }

    private static bool IsGeneratedAgentStartMarkdown(string content)
    {
        return content.StartsWith("# CARVES Agent Start", StringComparison.Ordinal);
    }

    private static bool IsGeneratedAgentStartJson(string content)
    {
        return content.Contains("\"schema_version\": \"carves.agent_start.v1\"", StringComparison.Ordinal);
    }

    private static bool IsGeneratedVisibleAgentStart(string content)
    {
        return content.StartsWith("# Start CARVES", StringComparison.Ordinal)
               && content.Contains("visible pointer for coding agents", StringComparison.Ordinal);
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
            // File mode support depends on the target filesystem. The launcher
            // content remains usable through an explicit shell even without chmod.
        }
    }

    private static string BashSingleQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
