namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeCliActivationPlanSurface
{
    public string SchemaVersion { get; init; } = "runtime-cli-activation-plan.v1";

    public string SurfaceId { get; init; } = "runtime-cli-activation-plan";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string ActivationGuideDocumentPath { get; init; } = string.Empty;

    public string InvocationGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot activation";

    public string JsonCommandEntry { get; init; } = "carves pilot activation --json";

    public string AliasCommandEntry { get; init; } = "carves pilot alias";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-cli-activation-plan";

    public string ApiCommandEntry { get; init; } = "carves api runtime-cli-activation-plan";

    public bool ActivationPlanComplete { get; init; }

    public string RecommendedActivationLane { get; init; } = string.Empty;

    public string RuntimeRootKind { get; init; } = string.Empty;

    public bool RuntimeRootHasPowerShellWrapper { get; init; }

    public bool RuntimeRootHasCmdWrapper { get; init; }

    public bool RuntimeRootHasDistManifest { get; init; }

    public bool RuntimeRootOnProcessPath { get; init; }

    public bool CarvesRuntimeRootEnvironmentMatches { get; init; }

    public int ActivationLaneCount { get; init; }

    public IReadOnlyList<RuntimeCliActivationLaneSurface> ActivationLanes { get; init; } = [];

    public IReadOnlyList<string> RequiredSmokeCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeCliActivationLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string ActivationMode { get; init; } = string.Empty;

    public string CommandPreview { get; init; } = string.Empty;

    public string Persistence { get; init; } = string.Empty;

    public string AppliesWhen { get; init; } = string.Empty;

    public string Boundary { get; init; } = string.Empty;

    public string RecommendedUse { get; init; } = string.Empty;
}
