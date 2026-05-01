namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeProductClosurePilotGuideSurface
{
    public string SchemaVersion { get; init; } = "runtime-product-closure-pilot-guide.v1";

    public string SurfaceId { get; init; } = "runtime-product-closure-pilot-guide";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string PreviousProofDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot guide";

    public string JsonCommandEntry { get; init; } = "carves pilot guide --json";

    public string StatusCommandEntry { get; init; } = "carves pilot status --json";

    public string AuthorityModel { get; init; } = "read_only_productized_pilot_guide";

    public string OfficialTruthIngressPolicy { get; init; } = "planner_review_and_host_writeback_only";

    public IReadOnlyList<RuntimeProductClosurePilotGuideStepSurface> Steps { get; init; } = [];

    public IReadOnlyList<string> CommitHygieneRules { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeProductClosurePilotGuideStepSurface
{
    public int Order { get; init; }

    public string StageId { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string AuthorityClass { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public string ExitSignal { get; init; } = string.Empty;
}
