using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static CodeUnderstandingEnginePolicy BuildDefaultPolicy()
    {
        return new CodeUnderstandingEnginePolicy
        {
            Summary = "Machine-readable code-understanding engine boundary for syntax substrate, structured query, semantic-index target correction, and qualification beyond memory-like heuristics.",
            ExtractionBoundary = new CodeUnderstandingExtractionBoundary
            {
                DirectAbsorptions =
                [
                    "incremental syntax-tree updates from tree-sitter-style parsing",
                    "syntax-error-tolerant parsing that still yields bounded structure facts",
                    "AST-structured query instead of ad hoc text search",
                    "bounded rewrite suggestions that stay behind review and approval",
                ],
                TranslatedIntoCarves =
                [
                    "repo-local codegraph truth under .ai/codegraph/",
                    "scope and impact analysis through ICodeGraphQueryService",
                    "review-bound codemod or rewrite suggestions instead of direct truth writeback",
                    "runtime qualification that separates syntax substrate, structured query, and semantic-index layers",
                ],
                RejectedAnchors =
                [
                    "the local D:/Projects/CARVES/scip-master checkout is the SCIP optimization solver, not the code-index protocol target",
                    "memory summaries as first-response structure truth",
                    "plain text grep as the primary code-understanding engine",
                ],
            },
            ConcernFamilies = BuildConcernFamilies(),
            PrecisionTiers = BuildPrecisionTiers(),
            SemanticPathPilots =
            [
                new CodeUnderstandingSemanticPathPilot
                {
                    PilotId = "bounded_csharp_semantic_path",
                    Language = "csharp",
                    CurrentStatus = "bounded_pilot_attached",
                    TargetPrecisionTiers =
                    [
                        "impact_grade",
                        "governance_grade",
                    ],
                    Summary = "Bounded C# semantic-path pilot for hotspot queues, refactoring admission, and policy-sensitive impact reads that need stronger evidence than search-grade lookup.",
                    ScopeRoots =
                    [
                        "src/CARVES.Runtime.Application/",
                        "src/CARVES.Runtime.Domain/",
                        "src/CARVES.Runtime.Infrastructure/CodeGraph/",
                        "tests/",
                    ],
                    GovernedEntryPoints =
                    [
                        "runtime-code-understanding-engine",
                        "materialize-refactors",
                        "inspect runtime-code-understanding-engine",
                    ],
                    EvidenceRefs =
                    [
                        ".ai/codegraph/modules/",
                        ".ai/codegraph/index.json",
                        ".ai/codegraph/dependencies/module-deps.json",
                        "ICodeGraphQueryService.AnalyzeImpact",
                    ],
                    Guardrails =
                    [
                        "pilot stays bounded to declared C# scope roots and follow-on workmap families",
                        "pilot complements codegraph-first truth instead of introducing a second semantic store",
                        "pilot supports admission and impact reads without claiming unrestricted symbol navigation",
                    ],
                    NonClaims =
                    [
                        "no Roslyn-grade whole-repo semantic coverage claim",
                        "no definition/reference/implementation truth claim outside the bounded pilot",
                    ],
                },
            ],
            TruthRoots = BuildTruthRoots(),
            BoundaryRules = BuildBoundaryRules(),
            GovernedReadPaths = BuildGovernedReadPaths(),
            Qualification = BuildQualificationLine(),
            Notes =
            [
                "This policy strengthens the existing .ai/codegraph line instead of introducing a second code-understanding store.",
                "tree-sitter and ast-grep are absorbed as bounded extraction references; CARVES-native truth remains repo-local and machine-readable.",
                "Precision tiers distinguish cheap lookup, bounded impact analysis, and governance-sensitive reads without claiming whole-repo semantic maturity.",
                "Semantic-index protocol work stays explicitly open until the correct upstream target is attached.",
            ],
        };
    }
}
