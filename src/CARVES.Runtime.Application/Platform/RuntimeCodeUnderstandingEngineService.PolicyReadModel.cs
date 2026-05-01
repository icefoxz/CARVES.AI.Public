using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static RuntimeKernelTruthRootDescriptor[] BuildTruthRoots()
    {
        return
        [
            Root(
                "codegraph_manifest_index",
                "derived_truth",
                "Manifest, index, and search projections remain the machine-readable first response for repo-local code structure facts.",
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
                "codegraph_module_summary_projection",
                "derived_truth",
                "Module and summary shards remain bounded human-readable projections derived from stronger codegraph facts.",
                [
                    ".ai/codegraph/modules/",
                    ".ai/codegraph/summaries/",
                ],
                [
                    "ICodeGraphQueryService.LoadModuleSummaries",
                    "ContextPackService.BuildModuleProjection",
                ]),
            Root(
                "codegraph_structure_fact_roots",
                "derived_truth",
                "Symbols, dependency shards, and impact projections remain the reserved roots for structure facts that should not fall back to memory as first truth.",
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
        ];
    }

    private static RuntimeKernelBoundaryRule[] BuildBoundaryRules()
    {
        return
        [
            Rule(
                "syntax_substrate_strengthens_codegraph_first",
                "Incremental syntax substrate should strengthen repo-local codegraph refresh and bounded reads instead of pushing structure truth back into memory-like heuristics.",
                [
                    ".ai/codegraph/",
                    "ICodeGraphBuilder.Build",
                    "FileCodeGraphBuilder.Build",
                ],
                [
                    "memory_as_first_structure_truth",
                    "second_codegraph_store",
                ]),
            Rule(
                "structured_query_is_ast_first_not_text_first",
                "Structured query and lint should remain syntax-shape-first instead of degrading into ad hoc text grep as the primary engine.",
                [
                    "ICodeGraphQueryService.AnalyzeScope",
                    "ICodeGraphQueryService.AnalyzeImpact",
                    ".ai/codegraph/search/",
                ],
                [
                    "plain_text_grep_as_primary_code_understanding_engine",
                ]),
            Rule(
                "bounded_rewrite_stays_review_bound",
                "Bounded rewrite or codemod output remains suggestion or review evidence until stronger approval and writeback gates accept it.",
                [
                    "review artifact",
                    "merge candidate artifact",
                    "approval gate",
                ],
                [
                    "direct_truth_writeback_from_rewrite_suggestion",
                ]),
            Rule(
                "local_scip_master_is_not_semantic_index_protocol",
                "The local scip-master checkout is the SCIP optimization solver and must not anchor semantic-index planning for code understanding.",
                [
                    "correct semantic-index protocol/indexer target",
                ],
                [
                    "D:/Projects/CARVES/scip-master as semantic index substrate",
                ]),
        ];
    }

}
