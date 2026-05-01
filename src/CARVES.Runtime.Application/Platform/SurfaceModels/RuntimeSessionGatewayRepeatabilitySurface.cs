using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSessionGatewayRecentTaskSurface
{
    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; }

    public string RecoveryAction { get; init; } = string.Empty;

    public string RecoveryReason { get; init; } = string.Empty;

    public bool ReviewArtifactAvailable { get; init; }

    public bool WorkerExecutionArtifactAvailable { get; init; }

    public bool ProviderArtifactAvailable { get; init; }

    public string ReviewEvidenceStatus { get; init; } = "unavailable";

    public bool ReviewCanFinalApprove { get; init; }

    public string ReviewEvidenceSummary { get; init; } = "(none)";

    public IReadOnlyList<string> MissingReviewEvidence { get; init; } = [];

    public string WorkerCompletionClaimStatus { get; init; } = "not_recorded";

    public bool WorkerCompletionClaimRequired { get; init; }

    public string WorkerCompletionClaimSummary { get; init; } = "(none)";

    public IReadOnlyList<string> MissingWorkerCompletionClaimFields { get; init; } = [];

    public IReadOnlyList<string> WorkerCompletionClaimEvidencePaths { get; init; } = [];

    public string WorkerCompletionClaimNextRecommendation { get; init; } = string.Empty;

    public string AcceptanceContractBindingState { get; init; } = "none";

    public string? AcceptanceContractId { get; init; }

    public string? AcceptanceContractStatus { get; init; }

    public string? ProjectedAcceptanceContractId { get; init; }

    public string? ProjectedAcceptanceContractStatus { get; init; }

    public IReadOnlyList<string> AcceptanceContractEvidenceRequired { get; init; } = [];
}

public sealed class RuntimeSessionGatewayTimelineEntrySurface
{
    public string EventKind { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? OperationId { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class RuntimeSessionGatewayRepeatabilitySurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-repeatability.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-repeatability";

    public string ExecutionPlanPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string RepeatabilityReadinessPath { get; init; } = string.Empty;

    public string DogfoodValidationPath { get; init; } = string.Empty;

    public string OperatorProofContractPath { get; init; } = string.Empty;

    public string AlphaSetupPath { get; init; } = string.Empty;

    public string AlphaQuickstartPath { get; init; } = string.Empty;

    public string KnownLimitationsPath { get; init; } = string.Empty;

    public string BugReportBundlePath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string PrivateAlphaHandoffPosture { get; init; } = string.Empty;

    public string DogfoodValidationPosture { get; init; } = string.Empty;

    public string ProgramClosureVerdict { get; init; } = string.Empty;

    public string ContinuationGateOutcome { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string RepeatabilityOwnership { get; init; } = "runtime_owned_repeatability";

    public string ThinShellRoute { get; init; } = string.Empty;

    public string SessionCollectionRoute { get; init; } = string.Empty;

    public string MessageRouteTemplate { get; init; } = string.Empty;

    public string EventsRouteTemplate { get; init; } = string.Empty;

    public string AcceptedOperationRouteTemplate { get; init; } = string.Empty;

    public string ProviderVisibilitySummary { get; init; } = string.Empty;

    public IReadOnlyList<string> ProviderStatuses { get; init; } = [];

    public IReadOnlyList<string> RecoveryCommands { get; init; } = [];

    public IReadOnlyList<string> ArtifactBundleCommands { get; init; } = [];

    public IReadOnlyList<string> RerunCommands { get; init; } = [];

    public IReadOnlyList<string> SupportedIntents { get; init; } = [];

    public IReadOnlyList<RuntimeSessionGatewayRecentTaskSurface> RecentGatewayTasks { get; init; } = [];

    public IReadOnlyList<RuntimeSessionGatewayTimelineEntrySurface> RecentTimelineEntries { get; init; } = [];

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
