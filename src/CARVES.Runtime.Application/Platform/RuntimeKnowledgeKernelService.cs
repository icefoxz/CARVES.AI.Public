using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeKnowledgeKernelService
{
    private readonly string repoRoot;

    private readonly ControlPlanePaths paths;

    public RuntimeKnowledgeKernelService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeKernelBoundarySurface Build()
    {
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-knowledge-kernel",
            KernelId = "knowledge",
            Summary = "Knowledge Kernel freezes capture, working memory, audit, promotion, and temporal fact history as explicit runtime roots while keeping task state and structure facts outside durable memory truth.",
            TruthRoots =
            [
                Root(
                    "durable_memory_truth",
                    "canonical_truth",
                    "Durable memory remains the long-lived human/AI knowledge corpus for architecture, project, module, and pattern guidance.",
                    [
                        ".ai/memory/architecture/",
                        ".ai/memory/project/",
                        ".ai/memory/modules/",
                        ".ai/memory/patterns/",
                    ],
                    [
                        "MemoryService",
                        "MemoryIndexer",
                    ]),
                Root(
                    "knowledge_inbox",
                    "canonical_truth",
                    "Inbox captures raw candidate knowledge before audit and promotion.",
                    [".ai/memory/inbox/"],
                    [
                        ".ai/memory/inbox/",
                        "runtime-knowledge-kernel",
                    ]),
                Root(
                    "session_memory",
                    "working_set",
                    "Session memory is bounded working memory for current operator or planner loops and is not durable project truth.",
                    [".ai/memory/session/"],
                    [
                        ".ai/memory/session/",
                        "runtime-knowledge-kernel",
                    ]),
                Root(
                    "memory_audit",
                    "audit_gate",
                    "Audit material records why a candidate memory item is allowed or denied promotion.",
                    [".ai/memory/audits/"],
                    [
                        ".ai/memory/audits/",
                        "runtime-knowledge-kernel",
                    ]),
                Root(
                    "memory_promotion",
                    "canonical_truth",
                    "Promotion records form the governed bridge from inbox/audit into durable memory truth.",
                    [".ai/memory/promotions/"],
                    [
                        ".ai/memory/promotions/",
                        "runtime-knowledge-kernel",
                    ]),
                Root(
                    "temporal_fact_truth",
                    "canonical_truth",
                    "Temporal fact records preserve provisional-to-canonical promotion history, validity windows, and supersession semantics for memory claims.",
                    [".ai/evidence/facts/"],
                    [
                        ".ai/evidence/facts/",
                        "RuntimeMemoryPromotionService",
                    ]),
                Root(
                    "execution_memory_truth",
                    "execution_history",
                    "Execution memory remains a separate truth family for run-linked summaries and must not be conflated with durable knowledge.",
                    [".ai/memory/execution/"],
                    [
                        "PlannerEmergenceService",
                        "execution_memory",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "promotion_requires_audit",
                    "Dream or consolidation remains bounded behind audit and promotion rather than writing directly into durable memory roots.",
                    [
                        ".ai/memory/inbox/",
                        ".ai/memory/audits/",
                        ".ai/memory/promotions/",
                    ],
                    [
                        "direct_write_from_session_to_durable_memory",
                        "unaudited_dream_merge",
                    ]),
                Rule(
                    "temporal_facts_preserve_validity_windows",
                    "Supersession or invalidation must close a fact validity window instead of deleting prior fact history.",
                    [
                        ".ai/evidence/facts/",
                        ".ai/memory/promotions/",
                    ],
                    [
                        "delete_prior_fact_on_promotion",
                        "erase_invalidated_fact_history",
                    ]),
                Rule(
                    "task_state_is_not_durable_memory",
                    "Task lifecycle truth, taskgraph topology, and execution state remain outside Knowledge Kernel durable memory roots.",
                    [
                        ".ai/tasks/",
                        ".ai/runtime/",
                    ],
                    [
                        "task_status_snapshots_inside_memory_modules",
                        "planner_state_inside_durable_memory",
                    ]),
                Rule(
                    "code_facts_live_in_codegraph",
                    "Structural code facts belong to codegraph-derived truth, not durable knowledge memory.",
                    [
                        ".ai/codegraph/",
                        "ICodeGraphQueryService",
                    ],
                    [
                        "symbol_tables_in_memory_modules",
                        "dependency_graph_in_memory_patterns",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "task_memory_bundle",
                    "MemoryService.BundleForTask",
                    "Task-scoped memory reads remain bundled and filtered rather than exposing the whole memory tree.",
                    [
                        "MemoryService",
                        ".ai/memory/modules/",
                        ".ai/memory/project/",
                    ]),
                ReadPath(
                    "context_pack_module_memory",
                    "inspect context-pack <task-id>",
                    "Context packs may reference module memory, but only as one bounded input alongside task and codegraph truth.",
                    [
                        "ContextPackService",
                        ".ai/memory/modules/",
                    ]),
                ReadPath(
                    "knowledge_kernel_surface",
                    "inspect runtime-knowledge-kernel",
                    "Operator surface explains which memory roots are durable, working, audited, or promotable.",
                    [
                        ".ai/memory/inbox/",
                        ".ai/memory/session/",
                        ".ai/memory/audits/",
                        ".ai/memory/promotions/",
                    ]),
                ReadPath(
                    "temporal_fact_ledger",
                    "RuntimeMemoryPromotionService.ListFacts",
                    "Promotion and invalidation history is queryable through temporal fact records instead of hidden markdown rewrites.",
                    [
                        ".ai/evidence/facts/",
                        ".ai/memory/promotions/",
                    ]),
            ],
            SuccessCriteria =
            [
                "Knowledge roots are explicit and runtime-queryable.",
                "Dream or consolidation is bounded behind audit and promotion.",
                "Temporal facts preserve validity and supersession history.",
                "Durable memory policy excludes task state and code-structure facts.",
            ],
            StopConditions =
            [
                "Do not rewrite existing durable memory documents during this baseline card.",
                "Do not treat execution memory as the same class as durable knowledge memory.",
                "Do not migrate codegraph structure facts into memory to satisfy search convenience.",
            ],
            Notes =
            [
                "This baseline materializes the missing roots so later knowledge-pipeline work has stable locations without claiming the full dream pipeline is implemented.",
                "Execution memory remains canonical runtime truth and is intentionally preserved outside the new knowledge roots.",
                "Promotion records and temporal facts explain what changed, when it changed, and what superseded it without rewriting durable markdown by default.",
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

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
