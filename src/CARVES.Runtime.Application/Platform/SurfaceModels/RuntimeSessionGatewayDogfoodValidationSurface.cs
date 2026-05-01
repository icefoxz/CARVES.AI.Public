using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSessionGatewayDogfoodValidationSurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-dogfood-validation.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-dogfood-validation";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string ExecutionPlanPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string OperatorProofContractPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string ProgramClosureVerdict { get; init; } = string.Empty;

    public string ContinuationGateOutcome { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string ThinShellPosture { get; init; } = "runtime_hosted_thin_shell";

    public string MutationForwardingPosture { get; init; } = "deferred";

    public string PrivateAlphaPosture { get; init; } = "deferred_until_mutation_forwarding";

    public string ThinShellRoute { get; init; } = string.Empty;

    public string SessionCollectionRoute { get; init; } = string.Empty;

    public string MessageRouteTemplate { get; init; } = string.Empty;

    public string EventsRouteTemplate { get; init; } = string.Empty;

    public string AcceptedOperationRouteTemplate { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedIntents { get; init; } = [];

    public IReadOnlyList<string> ValidatedScenarios { get; init; } = [];

    public IReadOnlyList<string> DeferredFollowOns { get; init; } = [];

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
