using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public static class RuntimeSemanticCorrectnessDeathTestEvidenceCatalog
{
    public static IReadOnlyList<RuntimeSemanticCorrectnessDeathTestPacketSurface> BuildEvidencePackets()
    {
        return
        [
            new()
            {
                PacketId = "death_test_1_direct_vs_governed",
                DeathTestId = "death_test_1",
                Focus = "controlled_flow_vs_direct_agent_flow",
                PrimaryDocPath = "docs/runtime/runtime-direct-agent-vs-governed-comparison-packet.md",
                RequiredEvidence = "shared input packet + shared success definition + shared failure classification + operator verdict",
                SuggestedCommands =
                [
                    RuntimeHostCommandLauncher.Cold("inspect", "runtime-semantic-correctness-death-test-evidence"),
                    RuntimeHostCommandLauncher.Cold("api", "runtime-semantic-correctness-death-test-evidence"),
                ],
                Summary = "Use Death Test 1 when the next question is whether governed CARVES flow improves real delivery outcomes over direct agent flow under the same input and success definition.",
            },
            new()
            {
                PacketId = "death_test_2_semantic_miss_interception",
                DeathTestId = "death_test_2",
                Focus = "direction_wrong_interception",
                PrimaryDocPath = "docs/runtime/runtime-semantic-miss-taxonomy.md",
                RequiredEvidence = "bounded miss category + interception stage + operator verdict + corrective action",
                SuggestedCommands =
                [
                    RuntimeHostCommandLauncher.Cold("inspect", "runtime-semantic-correctness-death-test-evidence"),
                    RuntimeHostCommandLauncher.Cold("inspect", "runtime-governance-program-reaudit"),
                ],
                Summary = "Use Death Test 2 when the question is whether CARVES intercepted bounded, test-passing work that was still semantically wrong.",
            },
            new()
            {
                PacketId = "death_test_3_control_plane_cost_vs_judgment_saved",
                DeathTestId = "death_test_3",
                Focus = "control_plane_cost_vs_judgment_saved",
                PrimaryDocPath = "docs/runtime/runtime-control-plane-cost-ledger.md",
                RequiredEvidence = "cost bucket capture + judgment saved narrative + evidence refs + non-billing-grade cost posture",
                SuggestedCommands =
                [
                    RuntimeHostCommandLauncher.Cold("inspect", "runtime-semantic-correctness-death-test-evidence"),
                    RuntimeHostCommandLauncher.Cold("api", "runtime-semantic-correctness-death-test-evidence"),
                ],
                Summary = "Use Death Test 3 when the question is whether the CARVES control plane saves enough human judgment and rework to justify its governance burden.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeSemanticMissCategorySurface> BuildSemanticMissCategories()
    {
        return
        [
            new()
            {
                CategoryId = "wrong_problem_clean_decomposition",
                InterceptionFocus = "problem_framing_or_planning",
                Summary = "The system decomposed the wrong ask cleanly.",
            },
            new()
            {
                CategoryId = "wrong_boundary_correct_local_move",
                InterceptionFocus = "planning_or_review",
                Summary = "The local change was coherent but landed in the wrong scope or boundary.",
            },
            new()
            {
                CategoryId = "test_passing_direction_wrong",
                InterceptionFocus = "review_or_post_writeback",
                Summary = "The implementation passed available tests while still moving the product in the wrong semantic direction.",
            },
            new()
            {
                CategoryId = "drifted_formal_context",
                InterceptionFocus = "planning_or_execution_review",
                Summary = "Authoritative-looking memory or rules pushed the run toward stale meaning.",
            },
            new()
            {
                CategoryId = "human_gate_repackaged_not_reduced",
                InterceptionFocus = "operator_approval_or_post_writeback",
                Summary = "The decisive semantic correction still came from a human despite a stronger governed process.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeControlPlaneCostBucketSurface> BuildControlPlaneCostBuckets()
    {
        return
        [
            new()
            {
                BucketId = "memory_upkeep",
                CostFocus = "summary freshness and memory correction",
                SavedJudgmentQuestion = "Did memory upkeep remove ambiguity or just relocate the ambiguity into formal maintenance work?",
            },
            new()
            {
                BucketId = "review_and_approval_overhead",
                CostFocus = "review-task, approval, rejection, and replan cycles",
                SavedJudgmentQuestion = "Did the extra review routing prevent hidden rework or merely add another checkpoint?",
            },
            new()
            {
                BucketId = "operator_proof_burden",
                CostFocus = "proof source declarations and operator evidence handoff",
                SavedJudgmentQuestion = "Did proof obligations materially clarify reality, or just add ceremony around work the operator would do anyway?",
            },
            new()
            {
                BucketId = "task_truth_maintenance",
                CostFocus = "card/task materialization and sync-state hygiene",
                SavedJudgmentQuestion = "Did task truth improve decision quality enough to offset the upkeep it introduces?",
            },
            new()
            {
                BucketId = "host_and_control_plane_friction",
                CostFocus = "host startup, repair, and substrate routing overhead",
                SavedJudgmentQuestion = "Did host-routed control materially reduce semantic risk or only shift operational burden into the control plane?",
            },
        ];
    }
}
