namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeCliInvocationContractSurface
{
    public string SchemaVersion { get; init; } = "runtime-cli-invocation-contract.v1";

    public string SurfaceId { get; init; } = "runtime-cli-invocation-contract";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string InvocationGuideDocumentPath { get; init; } = string.Empty;

    public string CliDistributionGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot invocation";

    public string JsonCommandEntry { get; init; } = "carves pilot invocation --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-cli-invocation-contract";

    public string ApiCommandEntry { get; init; } = "carves api runtime-cli-invocation-contract";

    public bool InvocationContractComplete { get; init; }

    public string RecommendedInvocationMode { get; init; } = string.Empty;

    public string RuntimeRootKind { get; init; } = string.Empty;

    public bool RuntimeRootHasPowerShellWrapper { get; init; }

    public bool RuntimeRootHasCmdWrapper { get; init; }

    public bool RuntimeRootHasDistManifest { get; init; }

    public bool RuntimeRootHasSolution { get; init; }

    public int InvocationLaneCount { get; init; }

    public IReadOnlyList<RuntimeCliInvocationLaneSurface> InvocationLanes { get; init; } = [];

    public IReadOnlyList<string> RequiredReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeCliInvocationLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string InvocationMode { get; init; } = string.Empty;

    public string CommandPattern { get; init; } = string.Empty;

    public string StabilityPosture { get; init; } = string.Empty;

    public string AppliesWhen { get; init; } = string.Empty;

    public string Boundary { get; init; } = string.Empty;

    public string RecommendedUse { get; init; } = string.Empty;
}
