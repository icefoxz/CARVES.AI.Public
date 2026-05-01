using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static CodeUnderstandingConcernFamily[] BuildConcernFamilies()
    {
        return
        [
            new CodeUnderstandingConcernFamily
            {
                FamilyId = "syntax_substrate",
                Layer = "syntax_substrate",
                Summary = "Syntax substrate is the incremental, syntax-error-tolerant foundation that strengthens repo-local codegraph refresh after small file edits.",
                CurrentStatus = "boundary_frozen_without_embedded_parser_runtime",
                SourceProjects = ["tree-sitter"],
                DirectAbsorptions =
                [
                    "incremental parsing semantics",
                    "small-edit tree update semantics",
                    "syntax-error-tolerant structure projection",
                ],
                TranslationTargets =
                [
                    ".ai/codegraph/manifest.json",
                    ".ai/codegraph/index.json",
                    ".ai/codegraph/modules/",
                    "FileCodeGraphBuilder refresh boundary",
                ],
                GovernedEntryPoints =
                [
                    "ICodeGraphBuilder.Build",
                    "FileCodeGraphBuilder.Build",
                    "scan-code",
                ],
                ReviewBoundaries =
                [
                    "syntax substrate produces structure facts, not direct task or review truth",
                ],
                OutOfScope =
                [
                    "semantic definition/reference/implementation navigation",
                    "review approval or writeback authority",
                ],
                Notes =
                [
                    "Current CARVES codegraph remains file-first; this boundary freezes the syntax substrate contract without claiming embedded parser integration in this slice.",
                ],
            },
            new CodeUnderstandingConcernFamily
            {
                FamilyId = "structured_query_and_rewrite",
                Layer = "structured_query",
                Summary = "Structured query and bounded rewrite sit above the syntax substrate and remain review-bound rather than direct writeback.",
                CurrentStatus = "boundary_frozen_without_auto_writeback",
                SourceProjects = ["ast-grep"],
                DirectAbsorptions =
                [
                    "AST-structured search",
                    "syntax-shape-based lint",
                    "bounded rewrite suggestion semantics",
                ],
                TranslationTargets =
                [
                    ".ai/codegraph/search/",
                    "ICodeGraphQueryService.AnalyzeScope",
                    "review-bound codemod suggestions",
                ],
                GovernedEntryPoints =
                [
                    "ICodeGraphQueryService.AnalyzeScope",
                    "ICodeGraphQueryService.AnalyzeImpact",
                    "audit codegraph --strict",
                ],
                ReviewBoundaries =
                [
                    "rewrite suggestions require review and approval before any writeback",
                    "structured query does not become an unrestricted refactor lane",
                ],
                OutOfScope =
                [
                    "semantic symbol navigation",
                    "automatic truth writeback from rewrite output",
                ],
            },
            new CodeUnderstandingConcernFamily
            {
                FamilyId = "semantic_index_protocol",
                Layer = "semantic_index",
                Summary = "Semantic index protocol remains the layer for definition/reference/implementation navigation, but the correct upstream target is not yet attached in this repo.",
                CurrentStatus = "target_corrected_but_unattached",
                SourceProjects = ["correct SCIP protocol/indexer target (not yet attached)"],
                DirectAbsorptions =
                [
                    "definition navigation concern family",
                    "reference navigation concern family",
                    "implementation navigation concern family",
                    "symbol navigation concern family",
                ],
                TranslationTargets =
                [
                    "future semantic index protocol surface",
                    "future codegraph symbol navigation truth",
                ],
                GovernedEntryPoints =
                [
                    "runtime-code-understanding-engine",
                    "runtime-domain-graph-kernel",
                ],
                ReviewBoundaries =
                [
                    "do not claim semantic index truth from the wrong local repository",
                ],
                OutOfScope =
                [
                    "D:/Projects/CARVES/scip-master as a code-index substrate",
                    "definition/reference/implementation truth claims before the correct target is attached",
                ],
                Notes =
                [
                    "The current local scip-master checkout is the SCIP optimization solver from scipopt/scip, not the code-index protocol project.",
                ],
            },
        ];
    }
}
