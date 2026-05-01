namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetDistBindingPlanSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-dist-binding-plan.v1";

    public string SurfaceId { get; init; } = "runtime-target-dist-binding-plan";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot dist-binding";

    public string JsonCommandEntry { get; init; } = "carves pilot dist-binding --json";

    public string AliasCommandEntry { get; init; } = "carves pilot bind-dist";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-target-dist-binding-plan";

    public string ApiCommandEntry { get; init; } = "carves api runtime-target-dist-binding-plan";

    public bool DistBindingPlanComplete { get; init; }

    public string RecommendedBindingMode { get; init; } = string.Empty;

    public string RuntimeRootKind { get; init; } = string.Empty;

    public bool TargetRuntimeInitialized { get; init; }

    public bool TargetBoundToLocalDist { get; init; }

    public bool TargetBoundToLiveSource { get; init; }

    public string CandidateDistRoot { get; init; } = string.Empty;

    public bool CandidateDistExists { get; init; }

    public bool CandidateDistHasManifest { get; init; }

    public bool CandidateDistHasVersion { get; init; }

    public bool CandidateDistHasWrapper { get; init; }

    public string CandidateDistVersion { get; init; } = string.Empty;

    public string CandidateDistSourceCommit { get; init; } = string.Empty;

    public bool CurrentRuntimeRootMatchesCandidateDist { get; init; }

    public string AttachHandshakeRuntimeRoot { get; init; } = string.Empty;

    public string RuntimeManifestRuntimeRoot { get; init; } = string.Empty;

    public IReadOnlyList<string> OperatorBindingCommands { get; init; } = [];

    public IReadOnlyList<string> RequiredReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
