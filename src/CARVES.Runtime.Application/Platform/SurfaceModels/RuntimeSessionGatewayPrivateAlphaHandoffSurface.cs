using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSessionGatewayPrivateAlphaHandoffSurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-private-alpha-handoff.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-private-alpha-handoff";

    public string ExecutionPlanPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string DogfoodValidationPath { get; init; } = string.Empty;

    public string OperatorProofContractPath { get; init; } = string.Empty;

    public string AlphaSetupPath { get; init; } = string.Empty;

    public string AlphaQuickstartPath { get; init; } = string.Empty;

    public string KnownLimitationsPath { get; init; } = string.Empty;

    public string BugReportBundlePath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string DogfoodValidationPosture { get; init; } = string.Empty;

    public string ProgramClosureVerdict { get; init; } = string.Empty;

    public string ContinuationGateOutcome { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string HandoffOwnership { get; init; } = "runtime_owned_private_alpha";

    public string ThinShellRoute { get; init; } = string.Empty;

    public string SessionCollectionRoute { get; init; } = string.Empty;

    public string MessageRouteTemplate { get; init; } = string.Empty;

    public string EventsRouteTemplate { get; init; } = string.Empty;

    public string AcceptedOperationRouteTemplate { get; init; } = string.Empty;

    public string RuntimeHealthState { get; init; } = string.Empty;

    public string RuntimeHealthSummary { get; init; } = string.Empty;

    public string RuntimeHealthSuggestedAction { get; init; } = string.Empty;

    public int RuntimeHealthIssueCount { get; init; }

    public int ProviderHealthIssueCount { get; init; }

    public int OptionalProviderHealthIssueCount { get; init; }

    public int DisabledProviderCount { get; init; }

    public string ProviderVisibilitySummary { get; init; } = string.Empty;

    public string OperationalRecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> ProviderStatuses { get; init; } = [];

    public IReadOnlyList<string> RuntimeIssueSummaries { get; init; } = [];

    public IReadOnlyList<string> StartupCommands { get; init; } = [];

    public IReadOnlyList<string> ProviderStatusCommands { get; init; } = [];

    public IReadOnlyList<string> RuntimeHealthCommands { get; init; } = [];

    public IReadOnlyList<string> MaintenanceCommands { get; init; } = [];

    public IReadOnlyList<string> BugReportBundleCommands { get; init; } = [];

    public IReadOnlyList<string> SupportedIntents { get; init; } = [];

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
