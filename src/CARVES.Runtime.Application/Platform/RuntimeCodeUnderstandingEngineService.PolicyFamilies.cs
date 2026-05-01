using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static CodeUnderstandingPrecisionTier[] BuildPrecisionTiers()
    {
        return
        [
            new CodeUnderstandingPrecisionTier
            {
                TierId = "search_grade",
                Summary = "Cheap lookup and lightweight structure retrieval grounded in repo-local codegraph facts.",
                PrimaryReadPath = "ICodeGraphQueryService.AnalyzeScope",
                EscalationTrigger = "Escalate when bounded impact or governance decisions need stronger dependency/path guarantees than fast file/index lookup provides.",
                EvidenceRefs =
                [
                    ".ai/codegraph/index.json",
                    ".ai/codegraph/modules/",
                    ".ai/codegraph/search/",
                ],
                NonClaims =
                [
                    "does not claim dependency-complete impact precision",
                    "does not authorize governance or refactoring admission by itself",
                ],
            },
            new CodeUnderstandingPrecisionTier
            {
                TierId = "impact_grade",
                Summary = "Stronger bounded impact analysis for planning, hotspot decomposition, and change-radius judgments.",
                PrimaryReadPath = "ICodeGraphQueryService.AnalyzeImpact",
                EscalationTrigger = "Escalate when a bounded task, hotspot queue, or code-understanding follow-on requires stronger dependency and scope guarantees.",
                EvidenceRefs =
                [
                    ".ai/codegraph/modules/",
                    ".ai/codegraph/dependencies/module-deps.json",
                    ".ai/codegraph/deps/",
                    ".ai/codegraph/impacts/",
                ],
                NonClaims =
                [
                    "does not claim whole-repo semantic symbol coverage",
                    "does not replace review-bound impact validation",
                ],
            },
            new CodeUnderstandingPrecisionTier
            {
                TierId = "governance_grade",
                Summary = "Proof-oriented precision used for policy, truth-writeback, and refactoring-admission decisions where bounded evidence must be explicit.",
                PrimaryReadPath = "audit codegraph --strict",
                EscalationTrigger = "Escalate when admission, proof, or governance surfaces would otherwise rely on search-grade or prose-only approximations.",
                EvidenceRefs =
                [
                    ".ai/codegraph/manifest.json",
                    ".ai/codegraph/index.json",
                    ".carves-platform/policies/code-understanding-engine.json",
                ],
                NonClaims =
                [
                    "does not claim Roslyn-grade whole-repo semantic maturity",
                    "does not bypass review, approval, or stronger policy gates",
                ],
            },
        ];
    }
}
