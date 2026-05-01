using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSemanticCorrectnessDeathTestEvidenceService
{
    private readonly string repoRoot;

    public RuntimeSemanticCorrectnessDeathTestEvidenceService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeSemanticCorrectnessDeathTestEvidenceSurface Build()
    {
        var errors = new List<string>();

        const string evidenceWorkmapPath = "docs/runtime/runtime-semantic-correctness-evidence-workmap.md";
        const string semanticCorrectnessDoctrinePath = "docs/runtime/runtime-semantic-correctness-gap-and-death-test-doctrine.md";
        const string officialTruthIngressDoctrinePath = "docs/runtime/runtime-central-interaction-point-and-official-truth-ingress.md";
        const string governanceAiPositioningPath = "docs/session-gateway/capability-forge-governance-ai-positioning.md";
        const string capabilityForgeRetirementRoutingPath = "docs/session-gateway/capability-forge-retirement-routing.md";
        const string runtimeGovernanceProgramReauditPath = "docs/runtime/runtime-governance-program-reaudit.md";
        const string directComparisonPacketPath = "docs/runtime/runtime-direct-agent-vs-governed-comparison-packet.md";
        const string semanticMissTaxonomyPath = "docs/runtime/runtime-semantic-miss-taxonomy.md";
        const string controlPlaneCostLedgerPath = "docs/runtime/runtime-control-plane-cost-ledger.md";

        ValidateDocument(evidenceWorkmapPath, "Semantic-correctness evidence workmap", errors);
        ValidateDocument(semanticCorrectnessDoctrinePath, "Semantic-correctness doctrine", errors);
        ValidateDocument(officialTruthIngressDoctrinePath, "Official truth ingress doctrine", errors);
        ValidateDocument(governanceAiPositioningPath, "Governance-AI positioning", errors);
        ValidateDocument(capabilityForgeRetirementRoutingPath, "Capability Forge retirement routing", errors);
        ValidateDocument(runtimeGovernanceProgramReauditPath, "Runtime governance program re-audit", errors);
        ValidateDocument(directComparisonPacketPath, "Direct-vs-governed comparison packet", errors);
        ValidateDocument(semanticMissTaxonomyPath, "Semantic-miss taxonomy", errors);
        ValidateDocument(controlPlaneCostLedgerPath, "Control-plane cost ledger", errors);

        var packets = RuntimeSemanticCorrectnessDeathTestEvidenceCatalog.BuildEvidencePackets();
        var categories = RuntimeSemanticCorrectnessDeathTestEvidenceCatalog.BuildSemanticMissCategories();
        var costBuckets = RuntimeSemanticCorrectnessDeathTestEvidenceCatalog.BuildControlPlaneCostBuckets();

        if (packets.Count != 3)
        {
            errors.Add($"Expected 3 death-test packets but found {packets.Count}.");
        }

        if (categories.Count < 5)
        {
            errors.Add($"Expected at least 5 semantic-miss categories but found {categories.Count}.");
        }

        if (costBuckets.Count < 5)
        {
            errors.Add($"Expected at least 5 control-plane cost buckets but found {costBuckets.Count}.");
        }

        var isValid = errors.Count == 0;

        return new RuntimeSemanticCorrectnessDeathTestEvidenceSurface
        {
            EvidenceWorkmapPath = evidenceWorkmapPath,
            SemanticCorrectnessDoctrinePath = semanticCorrectnessDoctrinePath,
            OfficialTruthIngressDoctrinePath = officialTruthIngressDoctrinePath,
            GovernanceAiPositioningPath = governanceAiPositioningPath,
            CapabilityForgeRetirementRoutingPath = capabilityForgeRetirementRoutingPath,
            RuntimeGovernanceProgramReauditPath = runtimeGovernanceProgramReauditPath,
            DirectAgentVsGovernedComparisonPacketPath = directComparisonPacketPath,
            SemanticMissTaxonomyPath = semanticMissTaxonomyPath,
            ControlPlaneCostLedgerPath = controlPlaneCostLedgerPath,
            OverallPosture = isValid ? "semantic_correctness_death_test_evidence_ready" : "blocked_by_semantic_correctness_evidence_gaps",
            DeathTestCount = packets.Count,
            SemanticMissCategoryCount = categories.Count,
            ControlPlaneCostBucketCount = costBuckets.Count,
            EvidencePackets = packets,
            SemanticMissCategories = categories,
            ControlPlaneCostBuckets = costBuckets,
            RecommendedNextAction = isValid
                ? "Use inspect/api runtime-semantic-correctness-death-test-evidence before opening 610-line or 611-line implementation so semantic-risk evidence stays explicit instead of implicit."
                : "Restore the missing doctrine/workmap packet files before claiming the semantic-correctness evidence line is ready.",
            IsValid = isValid,
            Errors = errors,
            NonClaims =
            [
                "This surface does not prove that CARVES already passes any death test.",
                "This surface does not create a second validation program, scheduler, or semantic-scoring engine.",
                "This surface does not convert process correctness, auditability, or host-routed writeback into semantic success proof.",
            ],
        };
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
