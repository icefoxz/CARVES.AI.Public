using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSessionGatewayInternalBetaGateSurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-internal-beta-gate.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-internal-beta-gate";

    public string ExecutionPlanPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string InternalBetaGatePath { get; init; } = string.Empty;

    public string RepeatabilityReadinessPath { get; init; } = string.Empty;

    public string OperatorProofContractPath { get; init; } = string.Empty;

    public string AlphaSetupPath { get; init; } = string.Empty;

    public string AlphaQuickstartPath { get; init; } = string.Empty;

    public string SuccessfulProofPacketPath { get; init; } = string.Empty;

    public string SuccessfulProofEvidencePath { get; init; } = string.Empty;

    public string PreviousFailurePacketPath { get; init; } = string.Empty;

    public string PreviousFailureEvidencePath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string PrivateAlphaHandoffPosture { get; init; } = string.Empty;

    public string RepeatabilityPosture { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string GateOwnership { get; init; } = "runtime_owned_internal_beta_gate";

    public string ThinShellRoute { get; init; } = string.Empty;

    public string SessionCollectionRoute { get; init; } = string.Empty;

    public string MessageRouteTemplate { get; init; } = string.Empty;

    public string EventsRouteTemplate { get; init; } = string.Empty;

    public string AcceptedOperationRouteTemplate { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedIntents { get; init; } = [];

    public IReadOnlyList<string> IncludedScope { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public IReadOnlyList<string> RequiredEvidenceBundle { get; init; } = [];

    public IReadOnlyList<string> EntryCommands { get; init; } = [];

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
