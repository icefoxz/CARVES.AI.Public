namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGovernedAgentHandoffProofSurface
{
    public string SchemaVersion { get; init; } = "runtime-governed-agent-handoff-proof.v1";

    public string SurfaceId { get; init; } = "runtime-governed-agent-handoff-proof";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProofDocumentPath { get; init; } = string.Empty;

    public string SessionGatewayDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string ProductClosureBaselineDocumentPath { get; init; } = string.Empty;

    public string ProductClosureCurrentDocumentPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string ProofScope { get; init; } = "bounded_runtime_agent_handoff";

    public string ActiveControlPlanePolicy { get; init; } = "runtime_single_control_plane";

    public string AdapterContractPosture { get; init; } = string.Empty;

    public string ProtectedTruthRootPosture { get; init; } = string.Empty;

    public string WorkingModeRecommendationPosture { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeGovernedAgentHandoffProofStageSurface> ProofStages { get; init; } = [];

    public IReadOnlyList<RuntimeGovernedAgentHandoffConstraintClassSurface> ConstraintClasses { get; init; } = [];

    public IReadOnlyList<string> RequiredColdReadbacks { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeGovernedAgentHandoffProofStageSurface
{
    public int Order { get; init; }

    public string StageId { get; init; } = string.Empty;

    public string RequiredSurfaceOrCommand { get; init; } = string.Empty;

    public string EvidenceProjected { get; init; } = string.Empty;

    public string Gate { get; init; } = string.Empty;
}

public sealed class RuntimeGovernedAgentHandoffConstraintClassSurface
{
    public string ClassId { get; init; } = string.Empty;

    public string EnforcementLevel { get; init; } = string.Empty;

    public string AppliesTo { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}
