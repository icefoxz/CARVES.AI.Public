using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeKernelUpgradeQualificationService
{
    public RuntimeKernelBoundarySurface Build()
    {
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-kernel-upgrade-qualification",
            KernelId = "qualification",
            Summary = "Kernel-upgraded runtime qualification freezes the explicit go/no-go gate for declaring the Context, Knowledge, Domain Graph, Execution, Artifact/Policy, and structure-convergence phases operational.",
            TruthRoots =
            [
                Root(
                    "execution_kernel_proof_path",
                    "proof_path",
                    "Execution Kernel is qualified only when actor/runtime boundaries, workspace lifecycle, and task/run truth remain queryable through one spine.",
                    [
                        "docs/runtime/POST_356_CHECKPOINT.md",
                        "docs/runtime/runtime-execution-kernel.md",
                    ],
                    [
                        "inspect runtime-execution-kernel",
                        "task inspect <task-id> --runs",
                        "api actor-sessions",
                    ]),
                Root(
                    "artifact_policy_proof_path",
                    "proof_path",
                    "Artifact and Policy kernels are qualified only when evidence, review, and gate decisions still flow through one bounded path.",
                    [
                        "docs/runtime/POST_357_CHECKPOINT.md",
                        "docs/runtime/runtime-artifact-policy-kernel.md",
                    ],
                    [
                        "inspect runtime-artifact-policy-kernel",
                        "policy inspect",
                        "inspect execution-contract-surface",
                    ]),
                Root(
                    "knowledge_codegraph_proof_path",
                    "proof_path",
                    "Knowledge and Domain Graph kernels are qualified only when memory, promotion, and codegraph-first structure reads remain distinct.",
                    [
                        "docs/runtime/POST_354_CHECKPOINT.md",
                        "docs/runtime/POST_355_CHECKPOINT.md",
                        "docs/runtime/runtime-knowledge-kernel.md",
                        "docs/runtime/runtime-domain-graph-kernel.md",
                    ],
                    [
                        "inspect runtime-knowledge-kernel",
                        "inspect runtime-domain-graph-kernel",
                        "audit codegraph --strict",
                    ]),
                Root(
                    "structure_freeze_proof_path",
                    "qualification_gate",
                    "Structure convergence is qualified only when maintainers can explain the upgraded repository through the kernel map without parallel truth stacks.",
                    [
                        "docs/runtime/POST_358_CHECKPOINT.md",
                        "docs/runtime/runtime-kernel-structure.md",
                        "README.md",
                        "docs/INDEX.md",
                    ],
                    [
                        "maintainer-first read order",
                        "canonical truth vs projection vs compatibility layers",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "go_requires_execution_and_knowledge_paths",
                    "Upgrade completion requires at least one explicit execution proof path and one explicit knowledge/codegraph proof path to remain queryable.",
                    [
                        "runtime-execution-kernel",
                        "runtime-knowledge-kernel",
                        "runtime-domain-graph-kernel",
                    ],
                    [
                        "qualification_without_multi_path_evidence",
                        "memory_only_completion_claim",
                    ]),
                Rule(
                    "no_go_if_parallel_truth_reappears",
                    "Any new duplicate truth owner for task, execution, memory, graph, artifact, or policy state blocks upgrade completion.",
                    [
                        "runtime-kernel-upgrade-program-boundary",
                        "runtime-core-adapter-boundary",
                    ],
                    [
                        "second_control_plane",
                        "parallel_kernel_truth_root",
                    ]),
                Rule(
                    "freeze_requires_structure_and_surface_alignment",
                    "Structure convergence docs and runtime query surfaces must describe the same kernel map before the program can declare success.",
                    [
                        "runtime-kernel-structure.md",
                        "inspect runtime-kernel-upgrade-qualification",
                        "docs/INDEX.md",
                    ],
                    [
                        "docs_only_without_runtime_surface",
                        "runtime_surface_without_maintainer_docs",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "upgrade_qualification_surface",
                    "inspect runtime-kernel-upgrade-qualification",
                    "Operator surface summarizes go/no-go criteria and proof paths for the kernel-upgrade program.",
                    [
                        "inspect runtime-kernel-upgrade-qualification",
                        "api runtime-kernel-upgrade-qualification",
                    ]),
                ReadPath(
                    "execution_kernel_path",
                    "inspect runtime-execution-kernel",
                    "Execution proof path remains governed through the execution kernel surface rather than a bespoke qualification stack.",
                    [
                        "inspect runtime-execution-kernel",
                        "task inspect <task-id> --runs",
                    ]),
                ReadPath(
                    "knowledge_graph_path",
                    "inspect runtime-knowledge-kernel | inspect runtime-domain-graph-kernel",
                    "Knowledge and codegraph proof paths stay governed through their existing kernel surfaces.",
                    [
                        "inspect runtime-knowledge-kernel",
                        "inspect runtime-domain-graph-kernel",
                        "audit codegraph --strict",
                    ]),
            ],
            SuccessCriteria =
            [
                "Go only if execution, artifact/policy, and knowledge/codegraph proof paths are all explicitly queryable.",
                "Go only if the maintainer-first structure docs and runtime surfaces point at the same kernel map.",
                "Go only if no parallel truth owner has been introduced during the upgrade.",
            ],
            StopConditions =
            [
                "No-go if a second control plane or second execution/review pipeline appears.",
                "No-go if code structure facts drift back into memory truth because codegraph reads are inconvenient.",
                "No-go if the repository structure becomes harder to explain than the pre-upgrade shape.",
            ],
            Notes =
            [
                "This qualification surface records explicit go/no-go criteria; it does not auto-promote the program or delete compatibility paths.",
            ],
        };
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
