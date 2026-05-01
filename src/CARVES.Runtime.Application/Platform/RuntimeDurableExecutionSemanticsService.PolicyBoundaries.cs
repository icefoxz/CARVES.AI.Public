using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeDurableExecutionSemanticsService
{
    private RuntimeKernelBoundaryRule[] BuildBoundaryRules()
    {
        return
        [
            Rule(
                "durable_execution_extends_existing_execution_spine",
                "Checkpoint, resume, interrupt, and state inspection extend the current task/run/review spine instead of creating a parallel graph-runtime truth hierarchy.",
                [
                    "TaskGraphService",
                    "ExecutionRunService",
                    "ReviewWritebackService",
                ],
                [
                    "parallel_graph_runtime_truth",
                    "second_execution_store",
                ]),
            Rule(
                "resume_remains_host_routed_and_ordered",
                "Resume semantics stay host-routed and ordered through explicit control and resume-gate surfaces.",
                [
                    "inspect async-resume-gate",
                    "session resume <reason...>",
                    "runtime resume <repo-id> <reason...>",
                ],
                [
                    "resume_without_host_or_gate",
                    "opaque_graph_replay",
                ]),
            Rule(
                "interrupt_and_inspection_preserve_current_review_gates",
                "Human interrupt and state inspection remain CARVES-native and preserve current review and approval gates.",
                [
                    "host pause <reason...>",
                    "runtime pause <repo-id> <reason...>",
                    "review-task",
                    "approve-review",
                ],
                [
                    "second_manual_control_plane",
                    "trace_only_interrupt_surface",
                ]),
            Rule(
                "working_memory_does_not_become_persistent_truth",
                "Execution working memory may support checkpoint and resume, but it must not absorb task truth, codegraph truth, or review truth into persistent memory ownership.",
                [
                    ".ai/runtime/context-packs/",
                    ".ai/runtime/execution-packets/",
                    ".ai/memory/session/",
                    ".ai/memory/promotions/",
                ],
                [
                    "task_truth_inside_persistent_memory",
                    "codegraph_truth_inside_persistent_memory",
                    "review_truth_inside_persistent_memory",
                ]),
            Rule(
                "taskgraph_replacement_is_rejected",
                "Durable execution qualification explicitly rejects direct TaskGraph replacement and generic graph-runtime flattening.",
                [
                    "runtime-execution-kernel",
                    "runtime-durable-execution-semantics",
                ],
                [
                    "replace_taskgraph_with_langgraph",
                    "generic_graph_runtime_flattening",
                ]),
        ];
    }

    private DurableExecutionReadinessEntry[] BuildReadinessMap()
    {
        return
        [
            Ready(
                "checkpoint_semantics",
                "ready_for_bounded_followup",
                "Checkpoint semantics are ready to extend current execution run and failure lineage without opening a second execution store.",
                [
                    "runtime-execution-kernel",
                    "task inspect <task-id> --runs",
                ]),
            Ready(
                "ordered_resume_semantics",
                "ready_for_bounded_followup",
                "Resume semantics are ready to extend current host/runtime control and async resume gate surfaces.",
                [
                    "inspect async-resume-gate",
                    "runtime resume <repo-id> <reason...>",
                    "session resume <reason...>",
                ]),
            Ready(
                "human_interrupt_and_state_inspection",
                "ready_for_bounded_followup",
                "Human interrupt and state inspection semantics are ready to extend current pause/review/runtime inspection surfaces.",
                [
                    "host pause <reason...>",
                    "runtime inspect <repo-id>",
                    "review-task",
                ]),
            Ready(
                "execution_working_vs_persistent_memory",
                "ready_for_bounded_followup",
                "Working-memory versus persistent-memory separation is ready to extend current knowledge and execution packet surfaces.",
                [
                    "runtime-knowledge-kernel",
                    "inspect context-pack <task-id>",
                    "inspect execution-packet <task-id>",
                ]),
            Ready(
                "multi_worker_durable_orchestration",
                "deferred_to_existing_lineage",
                "Multi-worker durable orchestration remains deferred to the existing async and delegation proof line.",
                [
                    "CARD-136",
                    "CARD-139",
                    "CARD-140",
                    "CARD-141",
                    "CARD-154",
                ]),
            Ready(
                "taskgraph_replacement",
                "rejected",
                "Direct TaskGraph replacement is explicitly rejected by this qualification line.",
                [
                    "runtime-execution-kernel",
                    "runtime-durable-execution-semantics",
                ]),
        ];
    }
}
