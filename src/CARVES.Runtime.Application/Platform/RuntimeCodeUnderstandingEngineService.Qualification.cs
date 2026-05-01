using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static RuntimeKernelGovernedReadPath[] BuildGovernedReadPaths()
    {
        return
        [
            ReadPath(
                "codegraph_scope_analysis",
                "ICodeGraphQueryService.AnalyzeScope",
                "Scope-sensitive code understanding should begin from structured codegraph scope analysis before memory summaries.",
                [
                    ".ai/codegraph/index.json",
                    ".ai/codegraph/modules/",
                    ".ai/codegraph/search/",
                ]),
            ReadPath(
                "codegraph_impact_analysis",
                "ICodeGraphQueryService.AnalyzeImpact",
                "Impact-sensitive reasoning should remain grounded in dependency and impact facts under .ai/codegraph.",
                [
                    ".ai/codegraph/dependencies/module-deps.json",
                    ".ai/codegraph/deps/",
                    ".ai/codegraph/impacts/",
                ]),
            ReadPath(
                "codegraph_strict_audit",
                "audit codegraph --strict",
                "Strict audit remains the bounded validation path for code-understanding truth instead of memory-like approximations.",
                [
                    ".ai/codegraph/manifest.json",
                    ".ai/codegraph/index.json",
                ]),
            ReadPath(
                "bounded_csharp_semantic_path_pilot",
                "runtime-code-understanding-engine bounded_csharp_semantic_path",
                "Impact-grade and governance-grade follow-on work may escalate into the bounded C# semantic-path pilot when codegraph-only lookup is insufficient.",
                [
                    ".ai/codegraph/modules/",
                    ".ai/codegraph/index.json",
                    ".carves-platform/policies/code-understanding-engine.json",
                ]),
        ];
    }

    private static CodeUnderstandingQualificationLine BuildQualificationLine()
    {
        return new CodeUnderstandingQualificationLine
        {
            Summary = "CARVES code-understanding is qualified when structure facts and bounded semantic-path escalation are answered from codegraph-first truth before memory summaries or operator prose.",
            ProvenLayers =
            [
                "syntax substrate boundary",
                "structured query and rewrite boundary",
                "semantic-index target correction",
                "precision tier boundary",
                "bounded C# semantic-path pilot",
                "qualification surface for code-understanding-first reasoning",
            ],
            FirstResponseRules =
            [
                "answer code structure from .ai/codegraph and governed codegraph services first",
                "use memory summaries or operator prose only as projection or explanation, not first structure truth",
                "escalate to impact_grade or governance_grade only when bounded follow-on work requires stronger evidence",
                "treat semantic-index navigation as future work until the correct target is attached",
            ],
            RemainingGaps =
            [
                "correct semantic-index protocol target is not yet attached locally",
                "this slice freezes syntax and structured-query boundaries without claiming embedded tree-sitter or ast-grep runtime integration",
                "definition/reference/implementation navigation remains future work",
            ],
            SuccessCriteria =
            [
                "The runtime surface distinguishes syntax substrate, structured query, and semantic-index concern families.",
                "The runtime surface explicitly defines search_grade, impact_grade, and governance_grade precision tiers.",
                "A bounded C# semantic-path pilot is attached for impact-grade and governance-grade follow-on reads.",
                "The local scip-master checkout is explicitly rejected as the semantic-index substrate.",
                "Qualification states that codegraph truth is grounded in code facts before memory-like heuristics.",
            ],
            StopConditions =
            [
                "Do not anchor semantic-index work to D:/Projects/CARVES/scip-master.",
                "Do not reclassify memory as authoritative structure truth.",
                "Do not overclaim the bounded C# pilot as whole-repo semantic maturity.",
                "Do not treat bounded rewrite suggestions as direct writeback authority.",
            ],
        };
    }
}
