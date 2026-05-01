namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAcceptanceContractIngressPolicySurface
{
    public string SchemaVersion { get; init; } = "runtime-acceptance-contract-ingress-policy.v1";

    public string SurfaceId { get; init; } = "runtime-acceptance-contract-ingress-policy";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyDocumentPath { get; init; } = string.Empty;

    public string SchemaPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string PlanningTruthMutationPolicy { get; init; } = string.Empty;

    public string ExecutionDispatchPolicy { get; init; } = string.Empty;

    public string PolicySummary { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeAcceptanceContractIngressLaneSurface> Ingresses { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAcceptanceContractIngressLaneSurface
{
    public string IngressId { get; init; } = string.Empty;

    public string LaneKind { get; init; } = string.Empty;

    public string ContractPolicy { get; init; } = string.Empty;

    public string MissingContractOutcome { get; init; } = string.Empty;

    public IReadOnlyList<string> Triggers { get; init; } = [];

    public IReadOnlyList<string> SourceAnchors { get; init; } = [];

    public string RecommendedAction { get; init; } = string.Empty;
}
