namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAdapterHandoffContractSurface
{
    public string SchemaVersion { get; init; } = "runtime-adapter-handoff-contract.v1";

    public string SurfaceId { get; init; } = "runtime-adapter-handoff-contract";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ContractDocumentPath { get; init; } = string.Empty;

    public string SessionGatewayDocumentPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string BaselineLaneId { get; init; } = "cli_first";

    public string AuthorityModel { get; init; } = "runtime_owned_contract_adapter_consumed";

    public string OfficialTruthIngressPolicy { get; init; } = "planner_review_and_host_writeback_only";

    public IReadOnlyList<RuntimeAdapterHandoffLaneSurface> Lanes { get; init; } = [];

    public IReadOnlyList<string> InspectCommands { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAdapterHandoffLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int PriorityOrder { get; init; }

    public string TransportPosture { get; init; } = string.Empty;

    public string RuntimeStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredInputs { get; init; } = [];

    public IReadOnlyList<string> RequiredOutputs { get; init; } = [];

    public IReadOnlyList<string> AllowedRuntimeCommands { get; init; } = [];

    public IReadOnlyList<string> NonAuthorityBoundaries { get; init; } = [];

    public string CompletionSignal { get; init; } = string.Empty;
}
