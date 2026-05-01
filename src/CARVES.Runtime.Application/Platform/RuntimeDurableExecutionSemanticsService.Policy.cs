using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeDurableExecutionSemanticsService
{
    private DurableExecutionSemanticsPolicy BuildDefaultPolicy()
    {
        return new DurableExecutionSemanticsPolicy
        {
            Summary = "Machine-readable durable execution semantics boundary derived from LangGraph durable execution, human-in-the-loop, checkpoint/resume, and memory-separation ideas without replacing CARVES TaskGraph or execution truth.",
            ExtractionBoundary = new DurableExecutionExtractionBoundary
            {
                DirectAbsorptions =
                [
                    "checkpoint semantics over the existing execution run and failure lineage",
                    "governed resume semantics with explicit ordered resume rules instead of opaque graph replay",
                    "human interrupt and operator-readable state inspection as first-class runtime concerns",
                    "working-memory versus persistent-memory separation for execution context",
                ],
                TranslatedIntoCarves =
                [
                    "policy truth under .carves-platform/policies/durable-execution-semantics.json",
                    "runtime-durable-execution-semantics inspect/api surfaces for operators and agents",
                    "resume and interrupt semantics attached to existing host, runtime, task, and review surfaces",
                    "memory-boundary guidance that keeps execution working memory separate from durable knowledge truth",
                ],
                RejectedAnchors =
                [
                    "replacing CARVES TaskGraph with LangGraph as the direct execution substrate",
                    "generic graph-runtime state as a second execution truth hierarchy",
                    "external tracing or graph replay as the primary operator truth source",
                ],
            },
            ConcernFamilies =
            [
                new DurableExecutionConcernFamily
                {
                    FamilyId = "checkpoint_semantics",
                    Layer = "checkpoint",
                    Summary = "Checkpoint semantics are absorbed as durable execution concepts over current task, run, and failure truth rather than as a generic graph snapshot store.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["LangGraph"],
                    DirectAbsorptions =
                    [
                        "durable checkpoint concept",
                        "re-entry after interruption or failure",
                        "checkpoint-aware failure context",
                    ],
                    TranslationTargets =
                    [
                        ".ai/runtime/runs/",
                        ".ai/runtime/run-reports/",
                        ".ai/failures/",
                        ".ai/memory/execution/",
                    ],
                    GovernedEntryPoints =
                    [
                        "task inspect <task-id> --runs",
                        "inspect runtime-execution-kernel",
                    ],
                    ReviewBoundaries =
                    [
                        "checkpoint evidence stays attached to the existing execution truth spine and does not become a second task store",
                    ],
                    OutOfScope =
                    [
                        "generic graph node snapshots",
                        "taskgraph replacement",
                    ],
                },
                new DurableExecutionConcernFamily
                {
                    FamilyId = "resume_semantics",
                    Layer = "resume",
                    Summary = "Resume semantics remain host-routed, ordered, and explicit so CARVES can resume work without generic graph-runtime flattening.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["LangGraph"],
                    DirectAbsorptions =
                    [
                        "explicit resume after interruption",
                        "resume order and gating",
                        "exact-resume versus replan distinction",
                    ],
                    TranslationTargets =
                    [
                        "inspect async-resume-gate",
                        "session resume <reason...>",
                        "runtime resume <repo-id> <reason...>",
                        "ExecutionRunTriggerReason.Resume",
                    ],
                    GovernedEntryPoints =
                    [
                        "inspect async-resume-gate",
                        "session resume <reason...>",
                        "runtime resume <repo-id> <reason...>",
                    ],
                    ReviewBoundaries =
                    [
                        "resume order remains host-routed and may not bypass review or approval gates",
                    ],
                    OutOfScope =
                    [
                        "generic graph replay engine",
                        "automatic async multi-worker orchestration proof",
                    ],
                },
                new DurableExecutionConcernFamily
                {
                    FamilyId = "human_interrupt_points",
                    Layer = "human_interrupt",
                    Summary = "Human interrupt remains a governed runtime control concern with explicit operator pause, review, and approval points.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["LangGraph"],
                    DirectAbsorptions =
                    [
                        "human-in-the-loop interruption",
                        "operator pause before resume",
                        "manual approval points before continuation",
                    ],
                    TranslationTargets =
                    [
                        "host pause <reason...>",
                        "runtime pause <repo-id> <reason...>",
                        "review-task",
                        "approve-review",
                    ],
                    GovernedEntryPoints =
                    [
                        "host pause <reason...>",
                        "runtime pause <repo-id> <reason...>",
                        "review-task <task-id> <verdict> <reason...>",
                    ],
                    ReviewBoundaries =
                    [
                        "interrupt points preserve the current approval and review gates instead of introducing a second manual control plane",
                    ],
                    OutOfScope =
                    [
                        "manual mutation outside host-routed truth writeback",
                    ],
                },
                new DurableExecutionConcernFamily
                {
                    FamilyId = "state_inspection_surfaces",
                    Layer = "state_inspection",
                    Summary = "State inspection stays CARVES-native and operator-readable through host/runtime/task surfaces rather than relying on an external tracing stack.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["LangGraph"],
                    DirectAbsorptions =
                    [
                        "inspectable runtime state",
                        "task and run lineage visibility",
                        "operator-readable state before and after interruption",
                    ],
                    TranslationTargets =
                    [
                        "task inspect <task-id> --runs",
                        "runtime inspect <repo-id>",
                        "actor sessions",
                        "api worker-leases",
                    ],
                    GovernedEntryPoints =
                    [
                        "task inspect <task-id> --runs",
                        "runtime inspect <repo-id>",
                        "actor sessions",
                    ],
                    ReviewBoundaries =
                    [
                        "state inspection remains a read surface and does not become a second truth owner",
                    ],
                    OutOfScope =
                    [
                        "external trace-only observability as the primary runtime truth",
                    ],
                },
                new DurableExecutionConcernFamily
                {
                    FamilyId = "execution_memory_separation",
                    Layer = "memory_boundary",
                    Summary = "Execution working memory and persistent memory remain separate so durable execution does not swallow task, codegraph, or review truth.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["LangGraph"],
                    DirectAbsorptions =
                    [
                        "short-term working memory for active execution context",
                        "persistent memory kept separate from active execution state",
                        "memory continuity that respects checkpoint and resume",
                    ],
                    TranslationTargets =
                    [
                        ".ai/runtime/context-packs/",
                        ".ai/runtime/execution-packets/",
                        ".ai/memory/session/",
                        ".ai/memory/promotions/",
                        "inspect runtime-knowledge-kernel",
                    ],
                    GovernedEntryPoints =
                    [
                        "inspect context-pack <task-id>",
                        "inspect execution-packet <task-id>",
                        "inspect runtime-knowledge-kernel",
                    ],
                    ReviewBoundaries =
                    [
                        "persistent memory does not gain ownership over task truth, codegraph truth, or review truth",
                    ],
                    OutOfScope =
                    [
                        "second memory truth hierarchy",
                        "persistent memory ownership of task lifecycle or codegraph structure facts",
                    ],
                },
            ],
            TruthRoots =
            [
                Root(
                    "execution_task_and_review_truth",
                    "canonical_truth",
                    "Task graph, review writeback, and execution memory remain the stronger durable execution truth spine for checkpoint-aware execution progress.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskNodesRoot),
                        ".ai/memory/execution/",
                    ],
                    [
                        "TaskGraphService",
                        "ReviewWritebackService",
                        "PlannerEmergenceService",
                    ]),
                Root(
                    "execution_run_and_failure_history",
                    "operational_history",
                    "Execution runs, run reports, and failure artifacts remain the replayable history layer for checkpoint and recovery semantics.",
                    [
                        ".ai/runtime/runs/",
                        ".ai/runtime/run-reports/",
                        ".ai/failures/",
                    ],
                    [
                        "ExecutionRunService",
                        "ExecutionRunReportService",
                        "FailureReportService",
                    ]),
                Root(
                    "resume_and_runtime_control_state",
                    "live_state",
                    "Runtime control state and host snapshots remain the governed live-state layer for pause, resume, and interruption posture.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.RuntimeSessionFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRuntimeStateRoot),
                    ],
                    [
                        "session status",
                        "runtime inspect <repo-id>",
                        "host status",
                    ]),
                Root(
                    "resume_gate_contract",
                    "boundary_contract",
                    "Ordered resume semantics remain governed through the async resume gate contract rather than generic graph replay state.",
                    [
                        "docs/contracts/async-resume-gate.schema.json",
                        "docs/runtime/async-multi-worker-resume-gate.md",
                    ],
                    [
                        "inspect async-resume-gate",
                        "ExecutionRunTriggerReason.Resume",
                    ]),
                Root(
                    "execution_working_memory",
                    "working_set",
                    "Execution working memory remains bounded to current execution context through context packs, execution packets, and session memory.",
                    [
                        ".ai/runtime/context-packs/",
                        ".ai/runtime/execution-packets/",
                        ".ai/memory/session/",
                    ],
                    [
                        "ContextPackService",
                        "ExecutionPacketCompilerService",
                        "runtime-agent-task-overlay",
                    ]),
                Root(
                    "persistent_knowledge_truth",
                    "canonical_truth",
                    "Persistent knowledge remains separate from active execution state and is governed by the existing knowledge kernel roots.",
                    [
                        ".ai/memory/architecture/",
                        ".ai/memory/project/",
                        ".ai/memory/modules/",
                        ".ai/memory/patterns/",
                        ".ai/memory/promotions/",
                    ],
                    [
                        "MemoryService",
                        "inspect runtime-knowledge-kernel",
                    ]),
                Root(
                    "codegraph_truth_dependency",
                    "stronger_truth_dependency",
                    "Codegraph structure facts remain outside persistent memory ownership even when durable execution needs structure-aware context.",
                    [
                        ".ai/codegraph/",
                    ],
                    [
                        "ICodeGraphQueryService",
                        "inspect runtime-code-understanding-engine",
                    ]),
            ],
            BoundaryRules = BuildBoundaryRules(),
            GovernedReadPaths =
            [
                ReadPath(
                    "durable_execution_surface",
                    "inspect runtime-durable-execution-semantics",
                    "Read the machine-readable durable execution boundary before proposing checkpoint, resume, interrupt, or memory-boundary changes.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformDurableExecutionSemanticsFile),
                    ]),
                ReadPath(
                    "checkpoint_and_run_lineage",
                    "task inspect <task-id> --runs",
                    "Task inspection remains the governed read path for checkpoint-aware execution lineage and replayable run history.",
                    [
                        ".ai/runtime/runs/",
                        ".ai/runtime/run-reports/",
                        ".ai/failures/",
                    ]),
                ReadPath(
                    "resume_gate_and_runtime_controls",
                    "inspect async-resume-gate | session resume <reason...> | runtime resume <repo-id> <reason...>",
                    "Resume semantics stay visible through explicit resume-gate and runtime-control surfaces.",
                    [
                        "docs/contracts/async-resume-gate.schema.json",
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.RuntimeSessionFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRuntimeStateRoot),
                    ]),
                ReadPath(
                    "human_interrupt_and_state_inspection",
                    "runtime inspect <repo-id> | actor sessions | api worker-leases",
                    "Human interrupt posture and current runtime ownership remain operator-readable through CARVES-native inspection surfaces.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRuntimeStateRoot),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformActorSessionsFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformWorkerLeasesFile),
                    ]),
                ReadPath(
                    "memory_boundary_read_path",
                    "inspect runtime-knowledge-kernel | inspect context-pack <task-id> | inspect execution-packet <task-id>",
                    "Working-memory versus persistent-memory decisions should start from existing knowledge and execution packet surfaces instead of prose summaries.",
                    [
                        ".ai/runtime/context-packs/",
                        ".ai/runtime/execution-packets/",
                        ".ai/memory/session/",
                        ".ai/memory/promotions/",
                    ]),
            ],
            Qualification = new DurableExecutionQualificationLine
            {
                Summary = "CARVES can absorb LangGraph-style durable execution ideas when they attach to current task/run/review/runtime truth, preserve Host-routed controls, and keep TaskGraph as the primary execution substrate.",
                SuccessCriteria =
                [
                    "Checkpoint, resume, human interrupt, state inspection, and memory separation are explicit concern families in machine-readable runtime truth.",
                    "Qualification explicitly rejects TaskGraph replacement and generic graph-runtime flattening.",
                    "A bounded next-step map exists for ready, deferred, and rejected durable execution semantics.",
                ],
                RejectedDirections =
                [
                    "TaskGraph replacement",
                    "generic graph-runtime flattening",
                    "external tracing as primary runtime truth",
                ],
                DeferredDirections =
                [
                    "async multi-worker orchestration proof remains in CARD-136 to CARD-154 lineage",
                    "generic graph node-state checkpointing is not opened in this slice",
                    "parallel durable execution scheduler beyond current host controls remains out of scope",
                ],
                StopConditions =
                [
                    "Do not replace CARVES TaskGraph with LangGraph.",
                    "Do not treat generic graph replay state as canonical execution truth.",
                    "Do not let persistent memory absorb task, codegraph, or review truth.",
                    "Do not claim async multi-worker parity from this qualification slice.",
                ],
            },
            ReadinessMap = BuildReadinessMap(),
            Notes =
            [
                "LangGraph is treated here as a durable-execution reference, not as a replacement for CARVES TaskGraph or truth ownership.",
                "The durable execution slice reuses existing host controls, async resume gate, runtime inspection, and knowledge kernel surfaces.",
            ],
        };
    }
}
