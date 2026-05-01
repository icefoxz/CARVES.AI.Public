using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentThreadStartService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeExternalTargetPilotStartSurface> pilotStartFactory;
    private readonly Func<RuntimeAgentProblemFollowUpPlanningGateSurface> followUpGateFactory;
    private readonly Func<RuntimeProductClosurePilotStatusSurface> pilotStatusFactory;
    private readonly Func<RuntimeGovernedAgentHandoffProofSurface> handoffProofFactory;

    public RuntimeAgentThreadStartService(
        string repoRoot,
        Func<RuntimeExternalTargetPilotStartSurface> pilotStartFactory,
        Func<RuntimeAgentProblemFollowUpPlanningGateSurface> followUpGateFactory,
        Func<RuntimeProductClosurePilotStatusSurface> pilotStatusFactory,
        Func<RuntimeGovernedAgentHandoffProofSurface> handoffProofFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.pilotStartFactory = pilotStartFactory;
        this.followUpGateFactory = followUpGateFactory;
        this.pilotStatusFactory = pilotStatusFactory;
        this.handoffProofFactory = handoffProofFactory;
    }

    public RuntimeAgentThreadStartSurface Build()
    {
        var startupReadback = BuildStartupReadback();
        if (ShouldUseLimitedPublicSnapshotStart(startupReadback))
        {
            return BuildLimitedPublicSnapshotStart(startupReadback);
        }

        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);

        var pilotStart = pilotStartFactory();
        var followUpGate = followUpGateFactory();
        var pilotStatus = pilotStatusFactory();
        var handoff = handoffProofFactory();
        errors.AddRange(pilotStart.Errors.Select(static error => $"runtime-external-target-pilot-start:{error}"));
        errors.AddRange(followUpGate.Errors.Select(static error => $"runtime-agent-problem-follow-up-planning-gate:{error}"));
        errors.AddRange(pilotStatus.Errors.Select(static error => $"runtime-product-closure-pilot-status:{error}"));
        errors.AddRange(handoff.Errors.Select(static error => $"runtime-governed-agent-handoff-proof:{error}"));

        var handoffReady = string.Equals(handoff.OverallPosture, "bounded_governed_agent_handoff_proof_ready", StringComparison.Ordinal);
        var runtimeReadbacksReady = errors.Count == 0
                                    && pilotStart.IsValid
                                    && pilotStart.PilotStartBundleReady
                                    && followUpGate.IsValid
                                    && followUpGate.PlanningGateReady
                                    && pilotStatus.IsValid
                                    && handoff.IsValid
                                    && handoffReady;
        var ready = runtimeReadbacksReady && startupReadback.StartupBoundaryReady;
        var next = ResolveNextCommand(followUpGate, pilotStatus, out var source);
        var discussionFirstSurface = string.Equals(source, "pilot_status", StringComparison.Ordinal)
                                     && IsDiscussionFirst(pilotStatus, next);
        var gaps = BuildGaps(runtimeReadbacksReady, errors, pilotStart, followUpGate, pilotStatus, handoff, handoffReady, startupReadback).ToArray();

        return new RuntimeAgentThreadStartSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            StartupEntrySource = startupReadback.StartupEntrySource,
            TargetProjectClassification = startupReadback.TargetProjectClassification,
            TargetClassificationOwner = startupReadback.TargetClassificationOwner,
            TargetClassificationSource = startupReadback.TargetClassificationSource,
            AgentTargetClassificationAllowed = false,
            TargetStartupMode = startupReadback.TargetStartupMode,
            ExistingProjectHandling = startupReadback.ExistingProjectHandling,
            StartupBoundaryReady = startupReadback.StartupBoundaryReady,
            StartupBoundaryPosture = startupReadback.StartupBoundaryPosture,
            StartupBoundaryGaps = startupReadback.StartupBoundaryGaps,
            TargetBoundRuntimeRoot = startupReadback.TargetBoundRuntimeRoot,
            TargetRuntimeBindingStatus = startupReadback.TargetRuntimeBindingStatus,
            TargetRuntimeBindingSource = startupReadback.TargetRuntimeBindingSource,
            AgentRuntimeRebindAllowed = false,
            RuntimeBindingRule = "If target Runtime binding is missing, mismatched, or requires rebind, stop and show CARVES output to the operator. Do not edit .ai/runtime.json or .ai/runtime/attach-handshake.json by hand.",
            WorkerExecutionBoundary = "null_worker_current_version_no_api_sdk_worker_execution",
            OverallPosture = ready
                ? "agent_thread_start_ready"
                : "agent_thread_start_blocked",
            ThreadStartReady = ready,
            NextGovernedCommand = next,
            NextCommandSource = source,
            DiscussionFirstSurface = discussionFirstSurface,
            AutoRunAllowed = false,
            RecommendedActionId = null,
            AvailableActions = ready && discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildSafeMenu()
                : [],
            ForbiddenAutoActions = ready && discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildForbiddenAutoActions()
                : [],
            PilotStartPosture = pilotStart.OverallPosture,
            PilotStartBundleReady = pilotStart.PilotStartBundleReady,
            PilotStatusPosture = pilotStatus.OverallPosture,
            CurrentStageId = pilotStatus.CurrentStageId,
            CurrentStageOrder = pilotStatus.CurrentStageOrder,
            CurrentStageStatus = pilotStatus.CurrentStageStatus,
            PilotStatusNextCommand = pilotStatus.NextCommand,
            FollowUpGatePosture = followUpGate.OverallPosture,
            FollowUpGateReady = followUpGate.PlanningGateReady,
            AcceptedPlanningItemCount = followUpGate.AcceptedPlanningItemCount,
            ReadyForPlanInitCount = followUpGate.ReadyForPlanInitCount,
            FollowUpGateNextCommand = followUpGate.NextGovernedCommand,
            HandoffPosture = handoff.OverallPosture,
            GovernedAgentHandoffReady = handoffReady,
            WorkingModeRecommendationPosture = handoff.WorkingModeRecommendationPosture,
            ProtectedTruthRootPosture = handoff.ProtectedTruthRootPosture,
            AdapterContractPosture = handoff.AdapterContractPosture,
            MinimalAgentRules = BuildMinimalAgentRules(),
            StopAndReportTriggers = pilotStart.StopAndReportTriggers,
            TroubleshootingReadbacks = BuildTroubleshootingReadbacks(),
            Gaps = gaps,
            Summary = BuildSummary(ready, next, source, pilotStatus, startupReadback),
            RecommendedNextAction = BuildRecommendedNextAction(ready, next, pilotStatus, startupReadback),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims = BuildNonClaims(),
        };
    }

    private bool ShouldUseLimitedPublicSnapshotStart(RuntimeAgentStartupReadback startupReadback)
    {
        return startupReadback.StartupBoundaryReady
               && !string.Equals(startupReadback.TargetProjectClassification, "runtime_operating_repo", StringComparison.Ordinal)
               && IsPublicSourceSnapshotRoot(documentRoot.DocumentRoot)
               && !File.Exists(RuntimeDocumentPath(RuntimeProductClosureMetadata.CurrentDocumentPath));
    }

    private RuntimeAgentThreadStartSurface BuildLimitedPublicSnapshotStart(RuntimeAgentStartupReadback startupReadback)
    {
        return new RuntimeAgentThreadStartSurface
        {
            ProductClosurePhase = "limited_public_snapshot_start",
            PhaseDocumentPath = "N/A:public_snapshot_private_closure_docs_not_packaged",
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            StartupEntrySource = startupReadback.StartupEntrySource,
            TargetProjectClassification = startupReadback.TargetProjectClassification,
            TargetClassificationOwner = startupReadback.TargetClassificationOwner,
            TargetClassificationSource = startupReadback.TargetClassificationSource,
            AgentTargetClassificationAllowed = false,
            TargetStartupMode = startupReadback.TargetStartupMode,
            ExistingProjectHandling = startupReadback.ExistingProjectHandling,
            StartupBoundaryReady = startupReadback.StartupBoundaryReady,
            StartupBoundaryPosture = startupReadback.StartupBoundaryPosture,
            StartupBoundaryGaps = startupReadback.StartupBoundaryGaps,
            TargetBoundRuntimeRoot = startupReadback.TargetBoundRuntimeRoot,
            TargetRuntimeBindingStatus = startupReadback.TargetRuntimeBindingStatus,
            TargetRuntimeBindingSource = startupReadback.TargetRuntimeBindingSource,
            AgentRuntimeRebindAllowed = false,
            RuntimeBindingRule = "If target Runtime binding is missing, mismatched, or requires rebind, stop and show CARVES output to the operator. Do not edit .ai/runtime.json or .ai/runtime/attach-handshake.json by hand.",
            WorkerExecutionBoundary = "null_worker_current_version_no_api_sdk_worker_execution",
            OverallPosture = "agent_thread_start_limited_public_snapshot_ready",
            ThreadStartReady = true,
            NextGovernedCommand = ".carves/carves gateway status",
            NextCommandSource = "limited_public_snapshot_visibility",
            DiscussionFirstSurface = false,
            AutoRunAllowed = false,
            RecommendedActionId = null,
            AvailableActions = BuildLimitedPublicSnapshotActions(),
            ForbiddenAutoActions = RuntimeDiscussionFirstSurfacePolicy.BuildForbiddenAutoActions(),
            PilotStartPosture = "not_evaluated_limited_public_snapshot",
            PilotStartBundleReady = false,
            PilotStatusPosture = "not_evaluated_limited_public_snapshot",
            CurrentStageId = "limited_public_snapshot_start",
            CurrentStageOrder = 0,
            CurrentStageStatus = "ready",
            PilotStatusNextCommand = ".carves/carves gateway status",
            FollowUpGatePosture = "not_evaluated_limited_public_snapshot",
            FollowUpGateReady = false,
            AcceptedPlanningItemCount = 0,
            ReadyForPlanInitCount = 0,
            FollowUpGateNextCommand = string.Empty,
            HandoffPosture = "not_evaluated_limited_public_snapshot",
            GovernedAgentHandoffReady = false,
            WorkingModeRecommendationPosture = "not_evaluated_limited_public_snapshot",
            ProtectedTruthRootPosture = "protected_truth_root_policy_not_evaluated_limited_public_snapshot",
            AdapterContractPosture = "adapter_contract_not_evaluated_limited_public_snapshot",
            MinimalAgentRules = BuildMinimalAgentRules(),
            StopAndReportTriggers = BuildLimitedPublicSnapshotStopAndReportTriggers(),
            TroubleshootingReadbacks = BuildTroubleshootingReadbacks(),
            Gaps = [],
            Summary = "Agent thread start is ready in limited public snapshot mode. Runtime startup and target binding are valid, but private product-closure/pilot proof surfaces are intentionally not evaluated.",
            RecommendedNextAction = "Use `.carves/carves gateway status` or `.carves/carves status --watch --iterations 1 --interval-ms 0` for visible readiness. Do not start worker execution, planning, review approval, writeback, commit, merge, or release from this limited readback.",
            IsValid = true,
            Errors = [],
            NonClaims = BuildLimitedPublicSnapshotNonClaims(),
        };
    }

    private static RuntimeInteractionActionSurface[] BuildLimitedPublicSnapshotActions()
    {
        return
        [
            new()
            {
                ActionId = "target_gateway_status",
                Label = "Target gateway status",
                Kind = "read_only",
                Command = ".carves/carves gateway status",
                Summary = "Read the target-bound CARVES gateway and Host status without planning, editing, or lifecycle writeback.",
            },
            new()
            {
                ActionId = "target_status_once",
                Label = "Target status once",
                Kind = "read_only",
                Command = ".carves/carves status --watch --iterations 1 --interval-ms 0",
                Summary = "Read one target-local status heartbeat without starting worker automation.",
            },
        ];
    }

    private static string[] BuildLimitedPublicSnapshotStopAndReportTriggers()
    {
        return
        [
            "a target-local visibility command fails or returns blocked posture",
            "the operator asks for worker execution, planning, review approval, truth writeback, commit, merge, or release",
            "the agent wants to modify protected truth roots or runtime binding files directly",
            "the agent cannot keep work to read-only visibility and ordinary discussion",
        ];
    }

    private static string[] BuildLimitedPublicSnapshotNonClaims()
    {
        return
        [
            "This limited public snapshot readback confirms startup binding and visibility only.",
            "This readback does not evaluate private product-closure, pilot proof, governed handoff, working-mode, or planning-gate surfaces.",
            "This readback does not grant worker execution authority, lifecycle truth, review approval, writeback, commit, merge, release, certification, hosted verification, or OS sandboxing.",
            "Public snapshot missing product-closure documents must not be repaired by copying private Runtime truth into the target project.",
        ];
    }

    private static string ResolveNextCommand(
        RuntimeAgentProblemFollowUpPlanningGateSurface followUpGate,
        RuntimeProductClosurePilotStatusSurface pilotStatus,
        out string source)
    {
        if (IsReadyForNewIntent(pilotStatus))
        {
            source = "pilot_status";
            return string.IsNullOrWhiteSpace(pilotStatus.NextCommand)
                ? "carves discuss context"
                : pilotStatus.NextCommand;
        }

        if (followUpGate.AcceptedPlanningItemCount > 0
            && !string.IsNullOrWhiteSpace(followUpGate.NextGovernedCommand))
        {
            source = "follow_up_planning_gate";
            return followUpGate.NextGovernedCommand;
        }

        source = "pilot_status";
        return string.IsNullOrWhiteSpace(pilotStatus.NextCommand)
            ? "carves pilot status --json"
            : pilotStatus.NextCommand;
    }

    private static bool IsReadyForNewIntent(RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        return string.Equals(pilotStatus.CurrentStageId, "ready_for_new_intent", StringComparison.Ordinal)
               || string.Equals(pilotStatus.OverallPosture, "pilot_status_product_pilot_proof_complete", StringComparison.Ordinal);
    }

    private static bool IsDiscussionFirst(RuntimeProductClosurePilotStatusSurface pilotStatus, string nextCommand)
    {
        return RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstStage(pilotStatus.CurrentStageId)
               || RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstCommand(nextCommand);
    }

    private static IEnumerable<string> BuildGaps(
        bool ready,
        IReadOnlyList<string> errors,
        RuntimeExternalTargetPilotStartSurface pilotStart,
        RuntimeAgentProblemFollowUpPlanningGateSurface followUpGate,
        RuntimeProductClosurePilotStatusSurface pilotStatus,
        RuntimeGovernedAgentHandoffProofSurface handoff,
        bool handoffReady,
        RuntimeAgentStartupReadback startupReadback)
    {
        if (ready && startupReadback.StartupBoundaryReady)
        {
            yield break;
        }

        foreach (var gap in startupReadback.StartupBoundaryGaps)
        {
            yield return $"startup_boundary:{gap}";
        }

        foreach (var error in errors)
        {
            yield return $"agent_thread_start_surface_error:{error}";
        }

        if (!pilotStart.PilotStartBundleReady)
        {
            yield return "pilot_start_bundle_not_ready";
        }

        if (!followUpGate.PlanningGateReady)
        {
            yield return "follow_up_planning_gate_not_ready";
        }

        if (!pilotStatus.IsValid)
        {
            yield return "pilot_status_not_valid";
        }

        if (!handoffReady)
        {
            yield return "governed_agent_handoff_not_ready";
        }

        foreach (var gap in pilotStart.Gaps)
        {
            yield return $"runtime-external-target-pilot-start:{gap}";
        }

        foreach (var gap in followUpGate.Gaps)
        {
            yield return $"runtime-agent-problem-follow-up-planning-gate:{gap}";
        }

        foreach (var gap in pilotStatus.Gaps)
        {
            yield return $"runtime-product-closure-pilot-status:{gap}";
        }
    }

    private static string[] BuildMinimalAgentRules()
    {
        return
        [
            "A new external-agent thread should run only `carves agent start --json` first.",
            "After that, treat `next_governed_command` as a legacy projection hint; prefer `available_actions` when present and do not auto-run any stateful step from that field alone.",
            "Inside a target project, prefer the project-local launcher recorded by the start packet; global `carves` is only a locator/dispatcher and must not replace target-bound `.carves/carves`.",
            "Use `.carves/carves gateway status` or `.carves/carves status --watch --iterations 1 --interval-ms 0` only as target-local visibility readbacks; they do not authorize worker execution.",
            "Do not classify the target project as new or old yourself; use `target_project_classification`, `target_classification_owner`, and `target_startup_mode` from this readback.",
            "Do not repair Runtime binding yourself; use `target_runtime_binding_status`, `target_bound_runtime_root`, and `runtime_binding_rule`, then stop for the operator if rebind is required.",
            "If any command is blocked, failed, ambiguous, or asks for protected-root edits, submit a bounded problem report with `carves pilot report-problem <json-path> --json`.",
            "Do not edit `.ai/tasks/`, `.ai/memory/`, `.ai/artifacts/reviews/`, `.carves-platform/`, runtime binding files, or git ignore policy directly.",
            "Use the troubleshooting readbacks only when the single start payload reports a gap or the operator asks for deeper evidence.",
        ];
    }

    private static string[] BuildTroubleshootingReadbacks()
    {
        return
        [
            ".carves/carves gateway status",
            ".carves/carves status --watch --iterations 1 --interval-ms 0",
            "carves pilot start --json",
            "carves pilot follow-up-gate --json",
            "carves pilot status --json",
            "carves agent handoff --json",
            "carves pilot problem-intake --json",
            "carves pilot target-proof --json",
            "carves pilot proof --json",
        ];
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface is read-only and does not initialize, plan, approve, write back, stage, commit, retarget, or mutate target files.",
            "This surface does not replace detailed troubleshooting readbacks; it selects the first actionable command for a new agent thread.",
            "This surface does not grant authority to classify targets manually, repair Runtime binding, or retarget `.ai/runtime.json` / attach-handshake files.",
            "This surface does not grant authority to bypass protected truth roots, planning gates, managed workspace gates, review gates, or commit-plan gates.",
            "This surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, public package distribution, or automatic updates.",
        ];
    }

    private static string BuildSummary(
        bool ready,
        string next,
        string source,
        RuntimeProductClosurePilotStatusSurface pilotStatus,
        RuntimeAgentStartupReadback startupReadback)
    {
        if (!startupReadback.StartupBoundaryReady)
        {
            return $"Agent thread start is blocked by target startup boundary: {startupReadback.StartupBoundaryPosture}. Stop and show CARVES output to the operator before planning or editing.";
        }

        if (!ready)
        {
            return "Agent thread start is blocked until the listed Runtime-owned readbacks are restored.";
        }

        if (IsDiscussionFirst(pilotStatus, next))
        {
            return $"Agent thread start is ready. Post-proof handoff is discussion-first: run only `{next}` next, clarify scope in normal chat, and open intent capture only after the operator/user gives a bounded engineering goal; source={source}.";
        }

        return $"Agent thread start is ready. `{next}` remains a legacy projection hint on this surface; prefer `available_actions` when present and keep stateful work behind the Host gate; source={source}.";
    }

    private static string BuildRecommendedNextAction(
        bool ready,
        string next,
        RuntimeProductClosurePilotStatusSurface pilotStatus,
        RuntimeAgentStartupReadback startupReadback)
    {
        if (!startupReadback.StartupBoundaryReady)
        {
            return startupReadback.StartupBoundaryRecommendedAction;
        }

        if (!ready)
        {
            return "Resolve the listed gaps, then rerun carves agent start --json.";
        }

        if (IsDiscussionFirst(pilotStatus, next))
        {
            return $"Run `{next}` first. Use normal chat to ask for the project's purpose, constraints, and whether new engineering work is actually requested; only after a bounded scope is explicit should you run `carves intent draft --persist`.";
        }

        return $"Use `{next}` only as the current projection hint. Prefer `available_actions` when present, and do not treat the legacy next-command field as standalone stateful authority.";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = RuntimeDocumentPath(repoRelativePath);
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private string RuntimeDocumentPath(string repoRelativePath)
    {
        return Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool IsPublicSourceSnapshotRoot(string root)
    {
        if (!File.Exists(Path.Combine(root, "START_CARVES.md"))
            || !File.Exists(Path.Combine(root, "carves")))
        {
            return false;
        }

        var readmePath = Path.Combine(root, "README.md");
        if (!File.Exists(readmePath))
        {
            return false;
        }

        var readme = File.ReadAllText(readmePath);
        return readme.Contains("public source snapshot", StringComparison.OrdinalIgnoreCase)
               || readme.Contains("public snapshot", StringComparison.OrdinalIgnoreCase);
    }

    private RuntimeAgentStartupReadback BuildStartupReadback()
    {
        var runtimeInitialized = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var repoHasRuntimeDocs = HasRuntimeDocumentBundle(repoRoot);
        var hasProjectLocalAgentStart = File.Exists(Path.Combine(repoRoot, RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath))
                                        || File.Exists(Path.Combine(repoRoot, RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath))
                                        || File.Exists(Path.Combine(repoRoot, RuntimeTargetAgentBootstrapPackService.VisibleAgentStartPath));
        var binding = RuntimeTargetBindingReadbackResolver.Resolve(
            repoRoot,
            documentRoot.DocumentRoot,
            "runtime_document_root");
        var startupGate = ResolveStartupBoundary(
            repoHasRuntimeDocs,
            runtimeInitialized,
            hasProjectLocalAgentStart,
            binding);

        return new RuntimeAgentStartupReadback(
            StartupEntrySource: hasProjectLocalAgentStart
                ? "target_project_agent_start"
                : repoHasRuntimeDocs
                    ? "runtime_repo_agent_start"
                    : "runtime_agent_start_without_project_local_packet",
            TargetProjectClassification: ResolveTargetProjectClassification(repoHasRuntimeDocs, runtimeInitialized),
            TargetClassificationOwner: runtimeInitialized || hasProjectLocalAgentStart
                ? "carves_up"
                : "operator",
            TargetClassificationSource: hasProjectLocalAgentStart
                ? RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath
                : runtimeInitialized
                    ? ".ai/runtime.json"
                    : "runtime_document_root_resolution",
            TargetStartupMode: ResolveTargetStartupMode(repoHasRuntimeDocs, runtimeInitialized, hasProjectLocalAgentStart, binding),
            ExistingProjectHandling: ResolveExistingProjectHandling(repoHasRuntimeDocs, runtimeInitialized, hasProjectLocalAgentStart, binding),
            StartupBoundaryReady: startupGate.Ready,
            StartupBoundaryPosture: startupGate.Posture,
            StartupBoundaryGaps: startupGate.Gaps,
            StartupBoundaryRecommendedAction: startupGate.RecommendedAction,
            TargetBoundRuntimeRoot: binding.BoundRuntimeRoot,
            TargetRuntimeBindingStatus: binding.Status,
            TargetRuntimeBindingSource: binding.Source);
    }

    private static string ResolveTargetProjectClassification(bool repoHasRuntimeDocs, bool runtimeInitialized)
    {
        if (repoHasRuntimeDocs)
        {
            return "runtime_operating_repo";
        }

        return runtimeInitialized
            ? "existing_carves_project"
            : "uninitialized_project";
    }

    private static string ResolveTargetStartupMode(
        bool repoHasRuntimeDocs,
        bool runtimeInitialized,
        bool hasProjectLocalAgentStart,
        RuntimeTargetBindingReadback binding)
    {
        if (repoHasRuntimeDocs)
        {
            return "runtime_repo_local";
        }

        if (!runtimeInitialized)
        {
            return "blocked_runtime_not_initialized";
        }

        if (BindingRequiresOperator(binding))
        {
            return "blocked_rebind_required";
        }

        return hasProjectLocalAgentStart
            ? "reuse_existing_runtime"
            : "initialized_without_project_local_agent_start_packet";
    }

    private static string ResolveExistingProjectHandling(
        bool repoHasRuntimeDocs,
        bool runtimeInitialized,
        bool hasProjectLocalAgentStart,
        RuntimeTargetBindingReadback binding)
    {
        if (repoHasRuntimeDocs)
        {
            return "runtime_repo_local_entry_not_target_classification";
        }

        if (!runtimeInitialized)
        {
            return "run_carves_up_or_init_before_target_work";
        }

        if (BindingRequiresOperator(binding))
        {
            return "operator_rebind_required_agent_must_stop";
        }

        return hasProjectLocalAgentStart
            ? "use_existing_carves_project_agent_start_readback"
            : "materialize_project_local_agent_start_packet_before_target_work";
    }

    private static bool BindingRequiresOperator(RuntimeTargetBindingReadback binding)
    {
        return binding.BlocksAgentStartup;
    }

    private static RuntimeAgentStartupGate ResolveStartupBoundary(
        bool repoHasRuntimeDocs,
        bool runtimeInitialized,
        bool hasProjectLocalAgentStart,
        RuntimeTargetBindingReadback binding)
    {
        if (repoHasRuntimeDocs)
        {
            return new RuntimeAgentStartupGate(
                true,
                "runtime_repo_startup_boundary_ready",
                [],
                "Follow CARVES readback output.");
        }

        if (!runtimeInitialized)
        {
            return new RuntimeAgentStartupGate(
                false,
                "target_startup_blocked_runtime_not_initialized",
                ["target_runtime_not_initialized", "carves_up_required_before_agent_start"],
                "Stop. Run `carves up <target-project>` from the known Runtime root before planning or editing this target.");
        }

        if (binding.BlocksAgentStartup)
        {
            return new RuntimeAgentStartupGate(
                false,
                "target_startup_blocked_by_runtime_binding",
                ["target_runtime_binding_not_ready", .. binding.Gaps],
                "Stop. Show `target_runtime_binding_status`, `target_bound_runtime_root`, and `runtime_binding_rule` to the operator; do not repair Runtime binding by hand.");
        }

        if (!hasProjectLocalAgentStart)
        {
            return new RuntimeAgentStartupGate(
                false,
                "target_startup_blocked_missing_project_local_agent_start_packet",
                ["project_local_agent_start_packet_missing", "carves_up_or_agent_bootstrap_write_required"],
                "Stop. Run `carves up <target-project>` from the known Runtime root, or use `carves agent bootstrap --write` only when the operator explicitly asks to repair the local start packet.");
        }

        return new RuntimeAgentStartupGate(
            true,
            "target_startup_boundary_ready",
            [],
            "Follow CARVES readback output.");
    }

    private static bool HasRuntimeDocumentBundle(string root)
    {
        return File.Exists(Path.Combine(root, "docs", "runtime", "runtime-governed-agent-handoff-proof.md"))
               && File.Exists(Path.Combine(root, "docs", "runtime", "runtime-first-run-operator-packet.md"));
    }

    private sealed record RuntimeAgentStartupReadback(
        string StartupEntrySource,
        string TargetProjectClassification,
        string TargetClassificationOwner,
        string TargetClassificationSource,
        string TargetStartupMode,
        string ExistingProjectHandling,
        bool StartupBoundaryReady,
        string StartupBoundaryPosture,
        IReadOnlyList<string> StartupBoundaryGaps,
        string StartupBoundaryRecommendedAction,
        string? TargetBoundRuntimeRoot,
        string TargetRuntimeBindingStatus,
        string TargetRuntimeBindingSource);

    private sealed record RuntimeAgentStartupGate(
        bool Ready,
        string Posture,
        IReadOnlyList<string> Gaps,
        string RecommendedAction);

}
