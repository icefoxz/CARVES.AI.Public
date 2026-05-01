using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSemanticCorrectnessDeathTestPacketSurface
{
    public string PacketId { get; init; } = string.Empty;
    public string DeathTestId { get; init; } = string.Empty;
    public string Focus { get; init; } = string.Empty;
    public string PrimaryDocPath { get; init; } = string.Empty;
    public string RequiredEvidence { get; init; } = string.Empty;
    public IReadOnlyList<string> SuggestedCommands { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeSemanticMissCategorySurface
{
    public string CategoryId { get; init; } = string.Empty;
    public string InterceptionFocus { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeControlPlaneCostBucketSurface
{
    public string BucketId { get; init; } = string.Empty;
    public string CostFocus { get; init; } = string.Empty;
    public string SavedJudgmentQuestion { get; init; } = string.Empty;
}

public sealed class RuntimeSemanticCorrectnessDeathTestEvidenceSurface
{
    public string SchemaVersion { get; init; } = "runtime-semantic-correctness-death-test-evidence.v1";
    public string SurfaceId { get; init; } = "runtime-semantic-correctness-death-test-evidence";
    public string EvidenceWorkmapPath { get; init; } = string.Empty;
    public string SemanticCorrectnessDoctrinePath { get; init; } = string.Empty;
    public string OfficialTruthIngressDoctrinePath { get; init; } = string.Empty;
    public string GovernanceAiPositioningPath { get; init; } = string.Empty;
    public string CapabilityForgeRetirementRoutingPath { get; init; } = string.Empty;
    public string RuntimeGovernanceProgramReauditPath { get; init; } = string.Empty;
    public string DirectAgentVsGovernedComparisonPacketPath { get; init; } = string.Empty;
    public string SemanticMissTaxonomyPath { get; init; } = string.Empty;
    public string ControlPlaneCostLedgerPath { get; init; } = string.Empty;
    public string OverallPosture { get; init; } = string.Empty;
    public string CurrentLine { get; init; } = "609_line_semantic_correctness_death_test_evidence";
    public string DeferredNextLine { get; init; } = "610_line_bootstrap_and_onboarding";
    public string ProgramClosureVerdict { get; init; } = "program_closure_complete";
    public int DeathTestCount { get; init; }
    public int SemanticMissCategoryCount { get; init; }
    public int ControlPlaneCostBucketCount { get; init; }
    public IReadOnlyList<RuntimeSemanticCorrectnessDeathTestPacketSurface> EvidencePackets { get; init; } = [];
    public IReadOnlyList<RuntimeSemanticMissCategorySurface> SemanticMissCategories { get; init; } = [];
    public IReadOnlyList<RuntimeControlPlaneCostBucketSurface> ControlPlaneCostBuckets { get; init; } = [];
    public string RecommendedNextAction { get; init; } = string.Empty;
    public bool IsValid { get; init; } = true;
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
