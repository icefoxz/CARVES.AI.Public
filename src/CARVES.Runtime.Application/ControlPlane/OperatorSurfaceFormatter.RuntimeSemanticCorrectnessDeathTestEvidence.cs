using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSemanticCorrectnessDeathTestEvidence(RuntimeSemanticCorrectnessDeathTestEvidenceSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime semantic-correctness death-test evidence",
            $"Evidence workmap doc: {surface.EvidenceWorkmapPath}",
            $"Semantic correctness doctrine doc: {surface.SemanticCorrectnessDoctrinePath}",
            $"Official truth ingress doctrine doc: {surface.OfficialTruthIngressDoctrinePath}",
            $"Governance-AI positioning doc: {surface.GovernanceAiPositioningPath}",
            $"Capability Forge retirement routing doc: {surface.CapabilityForgeRetirementRoutingPath}",
            $"Runtime governance program re-audit doc: {surface.RuntimeGovernanceProgramReauditPath}",
            $"Direct-vs-governed comparison packet doc: {surface.DirectAgentVsGovernedComparisonPacketPath}",
            $"Semantic-miss taxonomy doc: {surface.SemanticMissTaxonomyPath}",
            $"Control-plane cost ledger doc: {surface.ControlPlaneCostLedgerPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Current line: {surface.CurrentLine}",
            $"Deferred next line: {surface.DeferredNextLine}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Death tests: {surface.DeathTestCount}",
            $"Semantic-miss categories: {surface.SemanticMissCategoryCount}",
            $"Control-plane cost buckets: {surface.ControlPlaneCostBucketCount}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        foreach (var packet in surface.EvidencePackets)
        {
            lines.Add($"- packet:{packet.PacketId} death_test={packet.DeathTestId}");
            lines.Add($"  focus: {packet.Focus}");
            lines.Add($"  doc: {packet.PrimaryDocPath}");
            lines.Add($"  required_evidence: {packet.RequiredEvidence}");
            lines.Add($"  suggested_commands: {string.Join(" | ", packet.SuggestedCommands)}");
            lines.Add($"  summary: {packet.Summary}");
        }

        foreach (var category in surface.SemanticMissCategories)
        {
            lines.Add($"- miss_category:{category.CategoryId} interception_focus={category.InterceptionFocus}");
            lines.Add($"  summary: {category.Summary}");
        }

        foreach (var bucket in surface.ControlPlaneCostBuckets)
        {
            lines.Add($"- cost_bucket:{bucket.BucketId} focus={bucket.CostFocus}");
            lines.Add($"  saved_judgment_question: {bucket.SavedJudgmentQuestion}");
        }

        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
