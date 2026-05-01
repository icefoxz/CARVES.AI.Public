using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeContextKernelService
{
    private readonly string repoRoot;

    private readonly ControlPlanePaths paths;

    public RuntimeContextKernelService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeKernelBoundarySurface Build()
    {
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-context-kernel",
            KernelId = "context",
            Summary = "Context Kernel freezes bounded read context, context-pack assembly, and large-file narrowing as preflight runtime behavior rather than failure cleanup.",
            TruthRoots =
            [
                Root(
                    "context_pack_projection",
                    "projection",
                    "Task and planner context packs remain bounded runtime projections assembled from task, memory, failure, and codegraph truth.",
                    [
                        ToRepoRelative(Path.Combine(paths.RuntimeRoot, "context-packs")),
                        ToRepoRelative(Path.Combine(paths.RuntimeRoot, "execution-packets")),
                    ],
                    [
                        "ContextPackService",
                        "ExecutionPacketCompilerService",
                        "inspect context-pack <task-id>",
                        "inspect execution-packet <task-id>",
                    ]),
                Root(
                    "context_budget_policy",
                    "canonical_truth",
                    "Context budget policy remains explicit runtime truth and governs preflight narrowing before worker dispatch.",
                    [".ai/memory/architecture/07_CONTEXT_PACK_POLICY.md"],
                    [
                        "ContextBudgetPolicyResolver",
                        "WorkerAiRequestFactory",
                        "ExecutionPacketCompilerService",
                    ]),
                Root(
                    "bounded_read_context",
                    "canonical_truth",
                    "Bounded read context is shaped from task scope, codegraph analysis, and memory bundle reads rather than whole-repo prompt ingestion.",
                    [
                        "src/CARVES.Runtime.Application/AI/ContextPackService.cs",
                        "src/CARVES.Runtime.Application/Planning/PlannerContextAssembler.cs",
                        "src/CARVES.Runtime.Application/Workers/WorkerRequestFactory.cs",
                    ],
                    [
                        "ICodeGraphQueryService.AnalyzeScope",
                        "MemoryService.BundleForTask",
                    ],
                    [
                        "Large-file windowing is now projected into context-pack and execution-packet truth through bounded read metadata.",
                    ]),
                Root(
                    "windowed_read_projection",
                    "projection",
                    "Large-file windows and context compaction are projected into context-pack and execution-packet truth before delegated execution begins.",
                    [
                        "src/CARVES.Runtime.Application/AI/ContextPackService.cs",
                        "src/CARVES.Runtime.Application/Planning/ExecutionPacketCompilerService.cs",
                    ],
                    [
                        "inspect context-pack <task-id>",
                        "inspect execution-packet <task-id>",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "context_narrowing_preflight_first",
                    "Runtime must narrow context before dispatch when file volume, module breadth, or packet budget would overflow the bounded worker lane.",
                    [
                        "ContextBudgetPolicyResolver",
                        "ExecutionPacketCompilerService stop_conditions",
                        "WorkerAiRequestFactory patch_budget guidance",
                    ],
                    [
                        "whole_repo_prompt_ingestion",
                        "post_failure_context_shrink_as_primary_strategy",
                    ]),
                Rule(
                    "large_file_reads_remain_windowed_and_scoped",
                    "Context reads should stay task-scoped, module-scoped, or failure-scoped instead of reading entire large files into worker prompts by default.",
                    [
                        "ICodeGraphQueryService.AnalyzeScope",
                        "inspect context-pack <task-id>",
                        "inspect execution-packet <task-id>",
                    ],
                    [
                        "unbounded_large_file_copy_into_prompt",
                        "memory_as_structure_fallback",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "task_context_pack",
                    "inspect context-pack <task-id>",
                    "Projects the bounded task context that would be handed to a worker.",
                    [
                        "ContextPackService.BuildForTask",
                        ".ai/runtime/context-packs/tasks/",
                    ]),
                ReadPath(
                    "planner_context_pack",
                    "planner host/run surfaces",
                    "Planner wake handling assembles a bounded planner context instead of a whole-repo bundle.",
                    [
                        "ContextPackService.BuildForPlanner",
                        "PlannerContextAssembler",
                    ]),
                ReadPath(
                    "execution_packet_preflight",
                    "inspect execution-packet <task-id>",
                    "Execution packet inspection exposes budget, bounded-read compaction, and packet contracts before delegated execution starts.",
                    [
                        "ExecutionPacketCompilerService",
                        "WorkerAiRequestFactory",
                    ]),
            ],
            SuccessCriteria =
            [
                "Context Kernel concepts are queryable runtime truth rather than prompt-only guidance.",
                "The baseline exposes a minimal governed boundary for large-file and multi-file context loading.",
                "Runtime truth shows context narrowing and large-file compaction are preflight behavior, not failure cleanup.",
            ],
            StopConditions =
            [
                "Do not introduce a second context truth root outside existing runtime/context-pack and execution-packet projections.",
                "Do not replace codegraph or task scope analysis with memory-only prompt shaping.",
                "Do not widen context reads into whole-repo ingestion to satisfy a single task.",
            ],
            Notes =
            [
                "This baseline formalizes existing bounded-read behavior; it does not claim a separate Context Kernel repository or transport.",
                "Phase 1B deepens the baseline by projecting windowed reads and context compaction into context-pack and execution-packet truth.",
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
