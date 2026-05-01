using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeMinimalWorkerBaselineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeMinimalWorkerBaselineService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeMinimalWorkerBaselineSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeMinimalWorkerBaselineSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformMinimalWorkerBaselineFile),
            Policy = policy,
        };
    }

    public MinimalWorkerBaselinePolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }

    private MinimalWorkerBaselinePolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformMinimalWorkerBaselineFile))
        {
            var persisted = JsonSerializer.Deserialize<MinimalWorkerBaselinePolicy>(
                                File.ReadAllText(paths.PlatformMinimalWorkerBaselineFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformMinimalWorkerBaselineFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformMinimalWorkerBaselineFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private MinimalWorkerBaselinePolicy BuildDefaultPolicy()
    {
        var governance = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var weakLane = governance.WeakExecutionLanes.FirstOrDefault(item => string.Equals(item.LaneId, "weak-model-bounded-execution", StringComparison.Ordinal))
                       ?? new AgentGovernanceWeakExecutionLane
                       {
                           LaneId = "weak-model-bounded-execution",
                           Summary = "Weak models stay inside a bounded execution lane.",
                           ModelProfileId = "weak",
                           AllowedTaskTypes = ["execution"],
                           RequiredStartupSurfaces = ["runtime-agent-bootstrap-packet", "runtime-agent-task-overlay"],
                           RequiredRuntimeSurfaces = ["execution-packet", "execution-hardening", "runtime-agent-loop-stall-guard", "runtime-agent-model-profile-routing"],
                           RequiredVerification = ["targeted tests", "surface smoke", "post-change audit"],
                           ScopeCeiling = new AgentGovernanceScopeCeiling
                           {
                               MaxScopeEntries = 2,
                               MaxRelevantFiles = 8,
                               MaxFilesChanged = 4,
                               MaxLinesChanged = 200,
                           },
                           StopConditions = ["loop_guard_triggered", "predicted_patch_exceeds_budget", "touches_more_than_2_subsystems", "requires_new_card_or_taskgraph"],
                       };

        return new MinimalWorkerBaselinePolicy
        {
            Summary = "Machine-readable minimal worker baseline derived from mini-swe-agent lessons: linear query/action loop, per-action subprocess isolation, and weak-model suitability without replacing Host governance.",
            ExtractionBoundary = new MinimalWorkerExtractionBoundary
            {
                DirectAbsorptions =
                [
                    "minimal linear worker loop with explicit query and action phases",
                    "independent per-action subprocess execution instead of a hidden resident shell session",
                    "weak-model suitability through narrow scope, bounded verification, and strict stop conditions",
                ],
                TranslatedIntoCarves =
                [
                    "runtime-minimal-worker-baseline policy truth under .carves-platform/policies/",
                    "weak-model bounded execution lane projected through runtime-agent-weak-model-lane and task overlays",
                    "subprocess isolation as a bounded worker execution concern rather than a replacement for Host lifecycle truth",
                ],
                RejectedAnchors =
                [
                    "replacing CARVES Host with a mini worker loop",
                    "replacing TaskGraph or review-task/writeback gates with a direct worker substrate",
                    "treating worker simplicity as permission to skip Verification -> Boundary Gate -> Truth Writeback",
                ],
            },
            LinearLoop = new MinimalWorkerLinearLoopBaseline
            {
                Summary = "The thin worker loop stays linear: gather bounded prompt context, produce a query response, execute bounded actions, then return to stronger runtime gates.",
                LoopPhases = ["query", "execute_actions"],
                OrderingCompatibility =
                [
                    "The thin worker loop lives inside Implementation and never displaces Verification -> Boundary Gate -> Truth Writeback.",
                    "Linear execution is subordinate to task overlays, execution packets, and review-task completion.",
                ],
                OutsideWorkerLoop =
                [
                    "planner decomposition and replan policy",
                    "review and approval routing",
                    "task/card/taskgraph truth mutation",
                    "session and host lifetime governance",
                ],
                Notes =
                [
                    "mini-swe-agent is absorbed here as a worker-baseline reference, not as a second control plane.",
                ],
            },
            SubprocessBoundary = new MinimalWorkerSubprocessActionBoundary
            {
                Summary = "Each action may execute in an independent subprocess to strengthen isolation, replay, and sandboxing without confusing that action lifetime with the resident Host lifetime.",
                Benefits =
                [
                    "stateless per-action isolation",
                    "better sandbox and replay boundaries",
                    "reduced hidden shell session coupling",
                ],
                GuardedSeparations =
                [
                    "per-action subprocess lifetime is not resident Host lifetime",
                    "subprocess isolation does not become task truth or review truth",
                    "worker action execution remains subordinate to execution packets and boundary policy",
                ],
                RequiredGovernedState =
                [
                    "task run <task-id>",
                    "execution-packet truth",
                    "review-task",
                    "sync-state",
                ],
                OutOfScope =
                [
                    "replacing resident host orchestration",
                    "direct truth writeback from subprocess output",
                    "multi-worker orchestration claims",
                ],
            },
            WeakLane = new MinimalWorkerWeakLaneBoundary
            {
                LaneId = weakLane.LaneId,
                Summary = "Weak-model execution may borrow the minimal worker loop only inside the existing bounded weak lane.",
                ExecutionStyle = "linear_query_then_execute_actions_with_per_action_subprocess_isolation",
                ModelProfileId = weakLane.ModelProfileId,
                AllowedTaskTypes = weakLane.AllowedTaskTypes,
                RequiredStartupSurfaces = weakLane.RequiredStartupSurfaces,
                RequiredRuntimeSurfaces = weakLane.RequiredRuntimeSurfaces,
                RequiredVerification = weakLane.RequiredVerification,
                ScopeCeiling = weakLane.ScopeCeiling,
                StopConditions = weakLane.StopConditions,
                Notes =
                [
                    "Weak-lane execution inherits model-profile routing, loop/stall guard, and task overlay ceilings from the agent-governance kernel.",
                    "The minimal worker baseline strengthens weak execution reliability but does not raise weak models to deep-governance authority.",
                ],
            },
            Qualification = new MinimalWorkerQualificationLine
            {
                Summary = "CARVES gains a thinner and more predictable worker baseline when weak or low-context execution needs a minimal loop, but Host governance remains stronger than worker simplicity.",
                Gains =
                [
                    "clearer linear worker control flow for low-context execution",
                    "safer per-action isolation for shell-backed action execution",
                    "explicit weak-model suitability without broadening governance authority",
                ],
                SuccessCriteria =
                [
                    "The runtime surface distinguishes direct absorption, CARVES-native translation, and explicit rejection.",
                    "Linear worker phases remain query + execute_actions and are declared subordinate to Verification -> Boundary Gate -> Truth Writeback.",
                    "Qualification explicitly states that Host, TaskGraph, and writeback gates remain stronger governed surfaces.",
                ],
                IntentionallyOutsideScope =
                [
                    "Host replacement",
                    "TaskGraph replacement",
                    "planner/review simplification into the worker loop",
                    "claiming multi-job or async orchestration from the minimal baseline",
                ],
                StopConditions =
                [
                    "Do not let minimal-worker simplicity weaken host-routed truth writeback.",
                    "Do not flatten resident Host and subprocess action lifetime into one surface.",
                    "Do not treat weak-model bounded execution as deep-governance readiness.",
                ],
            },
            TruthRoots =
            [
                Root(
                    "minimal_worker_policy_truth",
                    "canonical_truth",
                    "The minimal worker baseline is frozen as machine-readable policy truth under .carves-platform/policies/.",
                    [
                        ".carves-platform/policies/minimal-worker-baseline.json",
                    ],
                    [
                        "inspect runtime-minimal-worker-baseline",
                        "api runtime-minimal-worker-baseline",
                    ]),
                Root(
                    "weak_lane_dependency_truth",
                    "canonical_truth_dependency",
                    "Weak-lane ceilings still inherit from the stronger agent-governance kernel.",
                    [
                        ".carves-platform/policies/agent-governance-kernel.json",
                    ],
                    [
                        "runtime-agent-weak-model-lane",
                        "runtime-agent-model-profile-routing",
                    ]),
                Root(
                    "host_taskgraph_writeback_truth",
                    "stronger_truth_dependency",
                    "TaskGraph, execution packet, review-task, and sync-state remain stronger execution truth than the thin worker loop.",
                    [
                        ".ai/tasks/",
                        ".ai/runtime/execution-packets/",
                        ".ai/memory/execution/",
                    ],
                    [
                        "task run <task-id>",
                        "review-task",
                        "sync-state",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "minimal_worker_loop_stays_inside_execution_runbook",
                    "A linear worker baseline may simplify worker internals, but it remains inside the stronger execution runbook phases owned by CARVES.",
                    [
                        "query",
                        "execute_actions",
                        "Verification -> Boundary Gate -> Truth Writeback",
                    ],
                    [
                        "worker_loop_as_complete_execution_pipeline",
                    ]),
                Rule(
                    "per_action_subprocess_does_not_replace_resident_host",
                    "Per-action subprocess execution strengthens isolation without replacing resident Host, runtime session truth, or review routing.",
                    [
                        "subprocess isolation",
                        "resident host",
                        "session truth",
                    ],
                    [
                        "subprocess_loop_as_host_replacement",
                    ]),
                Rule(
                    "weak_lane_remains_scope_bounded",
                    "Weak-model use of the minimal worker baseline remains bounded by the weak lane ceilings and stop conditions.",
                    [
                        "runtime-agent-task-overlay",
                        "runtime-agent-loop-stall-guard",
                        "runtime-agent-weak-model-lane",
                    ],
                    [
                        "broad_refactor_from_weak_lane",
                        "deep_governance_rewrite_from_weak_lane",
                    ]),
                Rule(
                    "host_taskgraph_and_writeback_remain_stronger",
                    "Host governance, TaskGraph truth, and truth writeback stay stronger than the worker baseline.",
                    [
                        "task run <task-id>",
                        "review-task",
                        "sync-state",
                    ],
                    [
                        "direct_truth_writeback_from_minimal_worker",
                        "taskgraph_replacement",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "minimal_worker_boundary_surface",
                    "inspect runtime-minimal-worker-baseline",
                    "Read the machine-readable extraction boundary before proposing worker simplification or weak-model baseline changes.",
                    [
                        ".carves-platform/policies/minimal-worker-baseline.json",
                    ]),
                ReadPath(
                    "weak_lane_overlay_path",
                    "inspect runtime-agent-task-overlay <task-id>",
                    "Thin worker execution remains task-overlay first and never broadens itself beyond the bounded lane.",
                    [
                        "runtime-agent-task-overlay",
                        "execution-packet",
                    ]),
                ReadPath(
                    "host_routed_execution_path",
                    "task run <task-id>",
                    "The minimal worker baseline still enters execution through host-routed task run rather than a standalone worker loop.",
                    [
                        "task run <task-id>",
                        "review-task",
                        "sync-state",
                    ]),
            ],
            Notes =
            [
                "mini-swe-agent is treated as a worker-baseline extraction source, not a control-plane replacement.",
                "The baseline stays intentionally thin so weak or low-context execution can start from fewer moving parts.",
            ],
        };
    }

    private static bool NeedsRefresh(MinimalWorkerBaselinePolicy policy)
    {
        return policy.PolicyVersion < 1
               || policy.LinearLoop.LoopPhases.Length < 2
               || !policy.LinearLoop.LoopPhases.Contains("query", StringComparer.Ordinal)
               || !policy.LinearLoop.LoopPhases.Contains("execute_actions", StringComparer.Ordinal)
               || policy.BoundaryRules.Length < 4
               || policy.GovernedReadPaths.Length < 3
               || !policy.ExtractionBoundary.RejectedAnchors.Contains(
                   "replacing CARVES Host with a mini worker loop",
                   StringComparer.Ordinal);
    }

    private static RuntimeKernelTruthRootDescriptor Root(
        string rootId,
        string classification,
        string summary,
        string[] pathRefs,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelTruthRootDescriptor
        {
            RootId = rootId,
            Classification = classification,
            Summary = summary,
            PathRefs = pathRefs,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelBoundaryRule Rule(
        string ruleId,
        string summary,
        string[] allowedRefs,
        string[] forbiddenRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelBoundaryRule
        {
            RuleId = ruleId,
            Summary = summary,
            AllowedRefs = allowedRefs,
            ForbiddenRefs = forbiddenRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelGovernedReadPath ReadPath(
        string pathId,
        string entryPoint,
        string summary,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelGovernedReadPath
        {
            PathId = pathId,
            EntryPoint = entryPoint,
            Summary = summary,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }
}
