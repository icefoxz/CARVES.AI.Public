using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentGovernanceKernelService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeAgentGovernanceKernelService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeAgentGovernanceKernelSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeAgentGovernanceKernelSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = ToRepoRelative(paths.PlatformAgentGovernanceKernelFile),
            Policy = policy,
        };
    }

    public AgentGovernanceKernelPolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }

    private AgentGovernanceKernelPolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformAgentGovernanceKernelFile))
        {
            var persisted = JsonSerializer.Deserialize<AgentGovernanceKernelPolicy>(
                                File.ReadAllText(paths.PlatformAgentGovernanceKernelFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformAgentGovernanceKernelFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformAgentGovernanceKernelFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private static AgentGovernanceKernelPolicy BuildDefaultPolicy()
    {
        return new AgentGovernanceKernelPolicy
        {
            Summary = "Machine-readable governance kernel for agent bootstrap, lifecycle classification, commit taxonomy, mixed-root caution, and fixed initialization/applied-governance output contracts.",
            GovernedEntryOrder =
            [
                "README",
                "AGENTS",
                "00_AI_ENTRY_PROTOCOL",
                "04_EXECUTION_RUNBOOK_CONTRACT",
                "05_EXECUTION_OS_METHODOLOGY",
                ".ai/PROJECT_BOUNDARY",
                ".ai/STATE",
                ".ai/TASK_QUEUE",
                "current_card_memory=<repo-relative list|N/A>",
                ".ai/DEV_LOOP",
            ],
            LifecycleClasses =
            [
                "CanonicalTruth",
                "GovernedMirror",
                "DerivedTruth",
                "OperationalHistory",
                "LiveState",
                "EphemeralResidue",
                "OutsideAiLifecycle",
                "MixedUmbrellaRoot",
                "UnclassifiedPath",
            ],
            CommitClasses =
            [
                "feature_commit",
                "truth_only_checkpoint",
                "local_residue",
                "not_a_normal_commit_target",
                "requires_governed_card_first",
            ],
            CorrectActions =
            [
                "commit",
                "restore",
                "cleanup",
                "compact-history",
                "rebuild",
                "keep_local",
                "host-routed-card-first",
                "operator_review_first",
            ],
            HostRoutedActions =
            [
                "create-card-draft",
                "approve-card",
                "create-taskgraph-draft",
                "approve-taskgraph-draft",
                "task run",
                "review-task",
                "approve-review",
                "sync-state",
                "audit sustainability",
                "compact-history",
            ],
            PathFamilies =
            [
                Family("bootstrap_anchor", ["AGENTS.md"], "OutsideAiLifecycle", "requires_governed_card_first", "host-routed-card-first", "Repo-bound bootstrap anchor outside .ai; semantic changes alter initialization or governance thresholds."),
                Family("agent_bootstrap_projection", ["CLAUDE.md", ".codex/"], "OutsideAiLifecycle", "requires_governed_card_first", "host-routed-card-first", "Agent-specific bootstrap projections remain downstream from repo truth and are never governed mirrors."),
                Family("task_truth", [".ai/tasks/"], "CanonicalTruth", "truth_only_checkpoint", "commit", "Task graph, nodes, and card markdown remain canonical task truth."),
                Family("execution_memory_truth", [".ai/memory/execution/"], "CanonicalTruth", "truth_only_checkpoint", "commit", "Governed execution memory remains canonical truth and must not be treated as cleanup residue."),
                Family("governed_markdown_mirrors", [".ai/STATE.md", ".ai/TASK_QUEUE.md", ".ai/CURRENT_TASK.md"], "GovernedMirror", "truth_only_checkpoint", "commit", "Governed markdown mirrors are checkpointable only after host-routed writeback."),
                Family("derived_runtime_context", [".ai/runtime/context-packs/"], "DerivedTruth", "not_a_normal_commit_target", "rebuild", "Context pack projections are rebuildable runtime-local truth derived from stronger sources."),
                Family("execution_history", [".ai/execution/"], "OperationalHistory", "local_residue", "compact-history", "Per-task execution envelopes remain reviewable operational history behind bounded compaction."),
                Family("planning_history", [".ai/runtime/planning/"], "OperationalHistory", "local_residue", "compact-history", "Planning history remains compactable operational history; top-level drafts are narrower residue exceptions."),
                Family("failure_history", [".ai/failures/"], "OperationalHistory", "local_residue", "compact-history", "Failure records remain inspectable operational history rather than commit-default truth."),
                Family("artifact_history", [".ai/artifacts/worker/", ".ai/artifacts/worker-executions/", ".ai/artifacts/provider/", ".ai/artifacts/reviews/"], "OperationalHistory", "local_residue", "compact-history", "Worker/provider/review artifacts are audit history, not normal feature commit surfaces."),
                Family("platform_policy_truth", [".carves-platform/policies/"], "CanonicalTruth", "feature_commit", "commit", "Stable control-plane policy and definition files are versioned policy truth."),
                Family("checkpoint_docs", ["docs/runtime/POST_xxx_CHECKPOINT.md"], "OutsideAiLifecycle", "feature_commit", "commit", "Checkpoint sidecar docs stay in docs/runtime by path even when task truth references them."),
                Family("ephemeral_spill", [".carves-temp/", ".ai/runtime/tmp/", ".ai/runtime/staging/", ".ai/artifacts/tmp/"], "EphemeralResidue", "local_residue", "cleanup", "Explicit temp or staging spill should be pruned instead of committed or archived."),
            ],
            MixedRoots =
            [
                new AgentGovernanceMixedRoot
                {
                    RootPath = ".carves-platform/runtime-state/",
                    Summary = "Mixed umbrella root: most children are live state, but routing truth and qualification ledgers remain narrower classified exceptions.",
                    ChildExceptions =
                    [
                        "active_routing_profile.json and candidate_routing_profile.json stay canonical routing truth",
                        "qualification_run_ledger.json remains operational history rather than live state",
                    ],
                },
                new AgentGovernanceMixedRoot
                {
                    RootPath = ".ai/runtime/planning/",
                    Summary = "Planning root stays operational history even though card-drafts/ and taskgraph-drafts/ can be narrower residue spill.",
                    ChildExceptions =
                    [
                        "card-drafts/ may be EphemeralResidue",
                        "taskgraph-drafts/ may be EphemeralResidue",
                    ],
                },
            ],
            GovernanceBoundaryDocPatterns =
            [
                "docs/runtime/runtime-artifact-boundary-normalization.md",
                "docs/runtime/runtime-commit-hygiene-governance.md",
                "docs/runtime/runtime-agent-projection-boundary.md",
                "docs/guides/AGENT_INITIALIZATION_REPORT_SPEC.md",
                "docs/guides/AGENT_BOOTSTRAP_COMPLIANCE_TEST.md",
                "docs/guides/AGENT_APPLIED_GOVERNANCE_TEST.md",
                "docs/guides/AGENT_PROJECT_KERNEL.md",
            ],
            UnclassifiedDefault = new AgentGovernanceDefaultBehavior
            {
                LifecycleClass = "UnclassifiedPath",
                CommitClass = "not_a_normal_commit_target",
                CorrectAction = "operator_review_first",
                Summary = "If a path family is outside the current boundary table and no narrower repo rule covers it, report it as unclassified instead of guessing cleanup or residue.",
            },
            InitializationContract = new AgentGovernanceInitializationContract
            {
                RequiredFields =
                [
                    "entry_path",
                    "truth_ownership",
                    "projection_boundary",
                    "host_routed_actions",
                    "runtime_state",
                    "not_yet_proven",
                    "initialization_verdict",
                ],
                SourceFields =
                [
                    "confirmed_sources",
                    "inferred_sources",
                    "unavailable_checks",
                ],
                EntryPathShape = "README -> AGENTS -> 00_AI_ENTRY_PROTOCOL -> 04_EXECUTION_RUNBOOK_CONTRACT -> 05_EXECUTION_OS_METHODOLOGY -> .ai/PROJECT_BOUNDARY -> .ai/STATE -> .ai/TASK_QUEUE -> current_card_memory=<repo-relative list|N/A> -> .ai/DEV_LOOP",
                RuntimeStateShape = "repo_state=<session/loop_mode/actionability|unknown>; host_snapshot=<Live/running|stopped|not_checked|unknown>; posture_basis=<repository_truth|dual_report|unknown>",
            },
            AppliedGovernanceContract = new AgentGovernanceAppliedGovernanceContract
            {
                TableColumns =
                [
                    "path",
                    "lifecycle_class",
                    "commit_class",
                    "correct_action",
                    "short_reason",
                ],
                VerdictFields =
                [
                    "bounded_work_ready -> yes|no",
                    "deep_governance_ready -> yes|no",
                    "reason -> ...",
                ],
                AutomaticFailConditions =
                [
                    "bootstrap assets classified as GovernedMirror",
                    "AGENTS.md or CLAUDE.md treated as sole truth owner",
                    ".carves-platform/runtime-state/ flattened into a single lifecycle class",
                    "docs/runtime/POST_xxx_CHECKPOINT.md classified by inbound reference instead of its own path",
                    "governance-boundary docs downgraded to ordinary feature_commit/commit",
                    "unclassified paths guessed as cleanup or local_residue instead of operator review first",
                    "SQLite or single-file migration proposed as the first response to CanonicalTruth growth pressure",
                ],
            },
            BootstrapPacketContract = new AgentGovernanceBootstrapPacketContract
            {
                Summary = "Low-context startup should default to a compact bootstrap packet derived from machine-readable governance truth and current repository posture.",
                StartupMode = "bootstrap_packet_default",
                RequiredPacketFields =
                [
                    "entry_order",
                    "current_card_memory_refs",
                    "report_fields",
                    "source_fields",
                    "repo_posture",
                    "host_snapshot",
                    "posture_basis",
                    "host_routed_actions",
                    "warm_resume_inspect_commands",
                    "not_yet_proven",
                ],
                DefaultInspectCommands =
                [
                    "inspect runtime-agent-bootstrap-packet",
                    "api runtime-agent-bootstrap-packet",
                ],
                OptionalDeepReadCommands =
                [
                    "inspect runtime-agent-governance-kernel",
                    "inspect runtime-agent-model-profile-routing",
                ],
            },
            WarmResumeContract = new AgentGovernanceWarmResumeContract
            {
                Summary = "Compressed-session re-entry should compare a prior governed receipt against current bootstrap truth so warm resume stays explicit, invalidatable, and secondary to cold init.",
                ResumeModes =
                [
                    "cold_init",
                    "warm_resume",
                ],
                RequiredReceiptFields =
                [
                    "policy_version",
                    "packet_surface_id",
                    "task_overlay_surface_id",
                    "repo_root",
                    "current_task_id",
                    "current_card_memory_refs",
                    "repo_posture",
                    "host_snapshot",
                    "posture_basis",
                    "verified_at",
                ],
                DefaultInspectCommands =
                [
                    "inspect runtime-agent-bootstrap-receipt [<receipt-json-path>]",
                    "api runtime-agent-bootstrap-receipt [<receipt-json-path>]",
                ],
                WarmResumeChecks =
                [
                    "compare prior receipt policy_version against current policy truth",
                    "compare prior receipt repo/task identity against current packet posture",
                    "compare prior receipt current_card_memory_refs against current packet refs",
                    "compare prior receipt repo_posture/host_snapshot/posture_basis against current packet posture",
                ],
                InvalidationTriggers =
                [
                    "prior receipt missing or unreadable",
                    "policy_version mismatch",
                    "repo_root mismatch",
                    "current_task_id mismatch",
                    "current_card_memory_refs mismatch",
                    "repo_posture mismatch",
                    "host_snapshot mismatch",
                    "posture_basis mismatch",
                ],
                ColdInitTriggers =
                [
                    "no prior governed receipt is available",
                    "warm resume comparison reports any mismatch",
                    "session posture is unknown or incomplete",
                ],
            },
            TaskOverlayContract = new AgentGovernanceTaskOverlayContract
            {
                Summary = "When task context exists, agents should prefer a compact task overlay over broad task queue reads.",
                RequiredOverlayFields =
                [
                    "task_id",
                    "card_id",
                    "scope_files",
                    "protected_roots",
                    "allowed_actions",
                    "required_verification",
                    "stable_evidence_surfaces",
                    "stop_conditions",
                ],
                DefaultInspectCommands =
                [
                    "inspect runtime-agent-task-overlay <task-id>",
                    "api runtime-agent-task-overlay <task-id>",
                ],
                SourceTruthRefs =
                [
                    "ExecutionPacket",
                    "TaskNode",
                    "runtime-agent-bootstrap-packet",
                ],
            },
            ModelProfiles =
            [
                new AgentGovernanceModelProfile
                {
                    ProfileId = "strong",
                    Summary = "Strong profile may consume the compact bootstrap packet and deeper runtime surfaces for bounded governance analysis.",
                    MaxStartupSources = 4,
                    GovernanceCeiling = "bounded_governance_analysis",
                    RequiresTaskOverlay = false,
                    DeepGovernanceReady = false,
                    PreferredBackendIds = ["codex_sdk"],
                    StartupSurfaces =
                    [
                        "runtime-agent-bootstrap-packet",
                        "runtime-agent-model-profile-routing",
                        "runtime-agent-governance-kernel",
                    ],
                    AllowedActions = ["bounded_analysis", "bounded_implementation", "applied_governance_judgment"],
                    ForbiddenActions = ["unsupervised_truth_substrate_migration"],
                },
                new AgentGovernanceModelProfile
                {
                    ProfileId = "standard",
                    Summary = "Standard profile defaults to compact startup inputs and bounded work without deep governance authority.",
                    MaxStartupSources = 3,
                    GovernanceCeiling = "bounded_work_only",
                    RequiresTaskOverlay = true,
                    DeepGovernanceReady = false,
                    PreferredBackendIds = ["codex_cli", "gemini_api", "openai_api"],
                    StartupSurfaces =
                    [
                        "runtime-agent-bootstrap-packet",
                        "runtime-agent-task-overlay",
                        "runtime-agent-model-profile-routing",
                    ],
                    AllowedActions = ["bounded_analysis", "bounded_implementation"],
                    ForbiddenActions = ["deep_governance_rewrite", "mixed_root_reclassification"],
                },
                new AgentGovernanceModelProfile
                {
                    ProfileId = "weak",
                    Summary = "Weak profile is restricted to compact startup inputs, explicit task overlay, and bounded execution lanes only.",
                    MaxStartupSources = 2,
                    GovernanceCeiling = "weak_lane_only",
                    RequiresTaskOverlay = true,
                    DeepGovernanceReady = false,
                    PreferredBackendIds = ["weak_lane_only"],
                    StartupSurfaces =
                    [
                        "runtime-agent-bootstrap-packet",
                        "runtime-agent-task-overlay",
                    ],
                    AllowedActions = ["bounded_analysis", "weak_bounded_execution"],
                    ForbiddenActions =
                    [
                        "deep_governance_rewrite",
                        "broad_refactor",
                        "truth_substrate_migration",
                    ],
                },
            ],
            LoopStallGuardPolicy = new AgentGovernanceLoopStallGuardPolicy
            {
                DetectorWindow = 5,
                DetectionLineage = "Reuse ExecutionPatternService detection and project forced outcomes by profile instead of creating a parallel loop detector.",
                StrictProfileIds = ["weak"],
                Rules =
                [
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "HealthyProgress",
                        StandardOutcome = "continue_within_budget",
                        WeakOutcome = "continue_within_budget",
                        Summary = "Healthy progress does not force an intervention.",
                    },
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "BoundaryLoop",
                        StandardOutcome = "narrow_scope",
                        WeakOutcome = "stop_and_replan",
                        Summary = "Repeated boundary loops should narrow scope, with weak profiles forced to stop and replan.",
                    },
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "ReplanLoop",
                        StandardOutcome = "change_replan_strategy",
                        WeakOutcome = "stop_and_replan",
                        Summary = "Repeated replan loops should change strategy, with weak profiles forced to stop and replan.",
                    },
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "RepeatedFailure",
                        StandardOutcome = "manual_review",
                        WeakOutcome = "stop_and_manual_review",
                        Summary = "Repeated failures should escalate to review, with weak profiles blocked from retry loops.",
                    },
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "ScopeDrift",
                        StandardOutcome = "narrow_scope",
                        WeakOutcome = "stop_and_narrow",
                        Summary = "Scope drift should narrow execution, with weak profiles forced to stop before continuing.",
                    },
                    new AgentGovernanceLoopStallRule
                    {
                        PatternType = "OverExecution",
                        StandardOutcome = "pause_and_review",
                        WeakOutcome = "stop_and_manual_review",
                        Summary = "Over-execution should pause or stop before more retries accumulate.",
                    },
                ],
            },
            WeakExecutionLanes =
            [
                new AgentGovernanceWeakExecutionLane
                {
                    LaneId = "weak-model-bounded-execution",
                    Summary = "Weak models may execute only inside a bounded lane with compact startup inputs, explicit task overlay, stronger verification, and strict stop conditions.",
                    ModelProfileId = "weak",
                    AllowedTaskTypes = ["execution"],
                    RequiredStartupSurfaces =
                    [
                        "runtime-agent-bootstrap-packet",
                        "runtime-agent-task-overlay",
                    ],
                    RequiredRuntimeSurfaces =
                    [
                        "execution-packet",
                        "execution-hardening",
                        "runtime-agent-loop-stall-guard",
                        "runtime-agent-model-profile-routing",
                    ],
                    RequiredVerification =
                    [
                        "targeted tests",
                        "surface smoke",
                        "post-change audit",
                    ],
                    ForbiddenCapabilities =
                    [
                        "deep_governance_rewrite",
                        "broad_refactor",
                        "mixed_root_reclassification",
                        "truth_substrate_migration",
                    ],
                    ScopeCeiling = new AgentGovernanceScopeCeiling
                    {
                        MaxScopeEntries = 2,
                        MaxRelevantFiles = 8,
                        MaxFilesChanged = 4,
                        MaxLinesChanged = 200,
                    },
                    StopConditions =
                    [
                        "loop_guard_triggered",
                        "predicted_patch_exceeds_budget",
                        "touches_more_than_2_subsystems",
                        "requires_new_card_or_taskgraph",
                    ],
                },
            ],
            NotYetProven =
            [
                "multi-job parity",
                "concurrent worker orchestration parity",
                "unrestricted patch materialization in closed lanes",
                "cryptographic proof that an agent is honest because it emitted the initialization report",
            ],
            Notes =
            [
                "Agent-facing markdown guides and prompt templates are projections of this policy truth, not parallel rule stacks.",
                "Use inspect/api runtime-agent-governance-kernel before making repo-wide governance judgments.",
                "Low-context startup should prefer inspect/api runtime-agent-bootstrap-packet and inspect/api runtime-agent-task-overlay before broad prose reloads.",
                "Compressed-session re-entry should prefer inspect/api runtime-agent-bootstrap-receipt before broad bootstrap rereads and should emit one final initialization report instead of a placeholder plus rewrite.",
            ],
        };
    }

    private static bool NeedsRefresh(AgentGovernanceKernelPolicy policy)
    {
        return policy.PolicyVersion < 3
               || policy.HostRoutedActions.Length == 0
               || string.IsNullOrWhiteSpace(policy.BootstrapPacketContract.SurfaceId)
               || string.IsNullOrWhiteSpace(policy.WarmResumeContract.SurfaceId)
               || string.IsNullOrWhiteSpace(policy.TaskOverlayContract.SurfaceId)
               || policy.ModelProfiles.Length == 0
               || policy.LoopStallGuardPolicy.Rules.Length == 0
               || policy.WeakExecutionLanes.Length == 0
               || !policy.Notes.Contains(
                   "Compressed-session re-entry should prefer inspect/api runtime-agent-bootstrap-receipt before broad bootstrap rereads and should emit one final initialization report instead of a placeholder plus rewrite.",
                   StringComparer.Ordinal);
    }

    private static AgentGovernancePathFamily Family(string familyId, string[] pathPatterns, string lifecycleClass, string commitClass, string correctAction, string summary)
    {
        return new AgentGovernancePathFamily
        {
            FamilyId = familyId,
            PathPatterns = pathPatterns,
            LifecycleClass = lifecycleClass,
            CommitClass = commitClass,
            CorrectAction = correctAction,
            Summary = summary,
        };
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
