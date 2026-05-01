using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class SessionGatewayArtifactWeightEntrySurface
{
    public string ArtifactKind { get; init; } = string.Empty;

    public string WeightClass { get; init; } = string.Empty;

    public int WeightScore { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}

public sealed class SessionGatewayChangePressureSurface
{
    public string PressureKind { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}

public sealed class SessionGatewayDecompositionCandidateSurface
{
    public string CandidateId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string BlockingState { get; init; } = string.Empty;

    public string PreferredProofSource { get; init; } = string.Empty;

    public string SuggestedAction { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}

public sealed class SessionGatewayReviewEvidencePlaybookEntrySurface
{
    public string PlaybookId { get; init; } = string.Empty;

    public string EvidenceKind { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public int BlockedTaskCount { get; init; }

    public IReadOnlyList<string> TaskIds { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string SuggestedAction { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}

public sealed class RuntimeSessionGatewayGovernanceAssistSurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-governance-assist.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-governance-assist";

    public string ExecutionPlanPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string RepeatabilityReadinessPath { get; init; } = string.Empty;

    public string GovernanceAssistPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string RepeatabilityPosture { get; init; } = string.Empty;

    public string ProgramClosureVerdict { get; init; } = string.Empty;

    public string ContinuationGateOutcome { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string GovernanceAssistOwnership { get; init; } = "runtime_owned_governance_assist";

    public string DynamicGateMode { get; init; } = "observe_assist";

    public bool ObserveOnly { get; init; } = true;

    public bool BlockingAuthority { get; init; }

    public string ProviderVisibilitySummary { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedIntents { get; init; } = [];

    public int ArtifactWeightTotal { get; init; }

    public int HighPressureCount { get; init; }

    public int RecentReviewTaskCount { get; init; }

    public int ReviewFinalReadyCount { get; init; }

    public int ReviewEvidenceBlockedCount { get; init; }

    public int ReviewEvidenceUnavailableCount { get; init; }

    public int WorkerCompletionClaimGapCount { get; init; }

    public int AcceptanceContractProjectedCount { get; init; }

    public int AcceptanceContractBindingGapCount { get; init; }

    public IReadOnlyList<SessionGatewayArtifactWeightEntrySurface> ArtifactWeightLedger { get; init; } = [];

    public IReadOnlyList<SessionGatewayChangePressureSurface> ChangePressures { get; init; } = [];

    public IReadOnlyList<SessionGatewayDecompositionCandidateSurface> DecompositionCandidates { get; init; } = [];

    public IReadOnlyList<SessionGatewayReviewEvidencePlaybookEntrySurface> ReviewEvidencePlaybook { get; init; } = [];

    public IReadOnlyList<RuntimeSessionGatewayRecentTaskSurface> RecentGatewayTasks { get; init; } = [];

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
