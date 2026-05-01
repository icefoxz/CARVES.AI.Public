using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeDomainGraphKernelService
{
    private readonly string repoRoot;

    public RuntimeDomainGraphKernelService(string repoRoot)
    {
        this.repoRoot = repoRoot;
    }

    public RuntimeKernelBoundarySurface Build()
    {
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-domain-graph-kernel",
            KernelId = "domain_graph",
            Summary = "Domain Graph Kernel freezes code-structure truth under .ai/codegraph and makes codegraph-first reads explicit for scope, dependency, and impact analysis.",
            TruthRoots =
            [
                Root(
                    "codegraph_manifest",
                    "derived_truth",
                    "Manifest and index files define the current generated codegraph contract for the attached repository.",
                    [
                        ".ai/codegraph/manifest.json",
                        ".ai/codegraph/index.json",
                        ".ai/codegraph/search/",
                    ],
                    [
                        "ICodeGraphQueryService.LoadManifest",
                        "ICodeGraphQueryService.LoadIndex",
                    ]),
                Root(
                    "codegraph_modules",
                    "derived_truth",
                    "Module and summary projections remain the primary human-readable structure facts for bounded reads.",
                    [
                        ".ai/codegraph/modules/",
                        ".ai/codegraph/summaries/",
                    ],
                    [
                        "ICodeGraphQueryService.LoadModuleSummaries",
                        "ContextPackService.BuildModuleProjection",
                    ]),
                Root(
                    "codegraph_structure_roots",
                    "derived_truth",
                    "Dedicated roots for symbols, dependencies, and impacts reserve stable locations for later graph refinement without shifting truth ownership back into memory.",
                    [
                        ".ai/codegraph/symbols/",
                        ".ai/codegraph/deps/",
                        ".ai/codegraph/impacts/",
                    ],
                    [
                        ".ai/codegraph/symbols/",
                        ".ai/codegraph/deps/",
                        ".ai/codegraph/impacts/",
                    ]),
                Root(
                    "codegraph_detail",
                    "derived_truth",
                    "Detailed graph truth is shard-backed via module shards and dependency data for strict audits and impact analysis.",
                    [
                        ".ai/codegraph/modules/",
                        ".ai/codegraph/dependencies/",
                    ],
                    [
                        "ICodeGraphQueryService.AnalyzeScope",
                        "ICodeGraphQueryService.AnalyzeImpact",
                        "audit codegraph --strict",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "structure_facts_are_codegraph_first",
                    "Symbol, dependency, summary, and impact facts should be answered from codegraph truth before falling back to hand-authored memory documents.",
                    [
                        "ICodeGraphQueryService",
                        "ContextPackService.BuildForTask",
                        "scan-code",
                        "audit codegraph --strict",
                    ],
                    [
                        "memory_as_authoritative_structure_source",
                        "manual_dependency_lists_as_primary_runtime_truth",
                    ]),
                Rule(
                    "codegraph_stays_repo_local_and_derived",
                    "Domain graph truth remains derived from the current repository and is regenerated rather than manually edited as primary truth.",
                    [
                        ".ai/codegraph/",
                        "scan-code",
                    ],
                    [
                        "manual_task_status_write_into_codegraph",
                        "provider_artifact_as_code_structure_source",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "scope_analysis",
                    "ICodeGraphQueryService.AnalyzeScope",
                    "Task and planner context assembly use codegraph scope analysis first when deciding relevant files and modules.",
                    [
                        "ContextPackService.BuildForTask",
                        "ContextPackService.BuildForPlanner",
                    ]),
                ReadPath(
                    "impact_analysis",
                    "ICodeGraphQueryService.AnalyzeImpact",
                    "Impact analysis remains the governed path for structure-sensitive change reasoning.",
                    [
                        "ICodeGraphQueryService.AnalyzeImpact",
                        ".ai/codegraph/dependencies/module-deps.json",
                    ]),
                ReadPath(
                    "codegraph_audit",
                    "audit codegraph --strict",
                    "Strict codegraph audit verifies the derived structure surface instead of relying on memory or prompt snapshots.",
                    [
                        "audit codegraph --strict",
                        ".ai/codegraph/manifest.json",
                        ".ai/codegraph/index.json",
                    ]),
            ],
            SuccessCriteria =
            [
                "Runtime surfaces make codegraph roots and governed reads explicit.",
                "Memory is documented and projected as non-authoritative for structure facts.",
                "At least one runtime read path is explicitly described as codegraph-first.",
            ],
            StopConditions =
            [
                "Do not move task, memory, or provider truth into .ai/codegraph.",
                "Do not introduce a parallel graph store outside the existing .ai/codegraph root.",
                "Do not claim the new symbols/deps/impacts roots are fully populated historical truth in this baseline card.",
            ],
            Notes =
            [
                "This baseline makes codegraph-first reads explicit while keeping codegraph derived and repo-local.",
                "The new sub-roots reserve clean structure for later graph refinement without forcing an immediate backfill migration.",
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
