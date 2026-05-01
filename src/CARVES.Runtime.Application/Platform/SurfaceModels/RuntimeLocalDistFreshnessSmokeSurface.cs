namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeLocalDistFreshnessSmokeSurface
{
    public string SchemaVersion { get; init; } = "runtime-local-dist-freshness-smoke.v1";

    public string SurfaceId { get; init; } = "runtime-local-dist-freshness-smoke";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string CurrentProductClosureDocumentPath { get; init; } = string.Empty;

    public string CurrentProductClosureGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot dist-smoke";

    public string JsonCommandEntry { get; init; } = "carves pilot dist-smoke --json";

    public string AliasCommandEntry { get; init; } = "carves pilot dist-freshness";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-local-dist-freshness-smoke";

    public string ApiCommandEntry { get; init; } = "carves api runtime-local-dist-freshness-smoke";

    public string DistVersion { get; init; } = RuntimeAlphaVersion.Current;

    public string SourceRepoRoot { get; init; } = string.Empty;

    public bool SourceRepoRootExists { get; init; }

    public bool SourceGitHeadDetected { get; init; }

    public string SourceGitHead { get; init; } = string.Empty;

    public bool SourceGitWorktreeClean { get; init; }

    public string CandidateDistRoot { get; init; } = string.Empty;

    public bool CandidateDistExists { get; init; }

    public bool CandidateDistHasManifest { get; init; }

    public bool CandidateDistHasVersion { get; init; }

    public bool CandidateDistHasWrapper { get; init; }

    public string CandidateDistPublishedCliEntry { get; init; } = RuntimeCliWrapperPaths.PublishedCliManifestEntry;

    public bool CandidateDistHasPublishedCli { get; init; }

    public bool CandidateDistHasPhaseDocument { get; init; }

    public bool CandidateDistHasGuideDocument { get; init; }

    public bool CandidateDistHasTargetBindingGuide { get; init; }

    public bool CandidateDistHasLocalDistGuide { get; init; }

    public bool CandidateDistHasCliDistributionGuide { get; init; }

    public bool CandidateDistHasCurrentProductClosureDocument { get; init; }

    public bool CandidateDistHasCurrentProductClosureGuide { get; init; }

    public bool CandidateDistHasGitDirectory { get; init; }

    public bool CandidateDistHasSolution { get; init; }

    public string ManifestSchemaVersion { get; init; } = string.Empty;

    public string ManifestVersion { get; init; } = string.Empty;

    public string ManifestSourceCommit { get; init; } = string.Empty;

    public string ManifestSourceRepoRoot { get; init; } = string.Empty;

    public string ManifestOutputPath { get; init; } = string.Empty;

    public string ManifestPublishedCliEntry { get; init; } = string.Empty;

    public string VersionFileValue { get; init; } = string.Empty;

    public bool ManifestVersionMatchesVersionFile { get; init; }

    public bool ManifestOutputMatchesCandidateDist { get; init; }

    public bool ManifestSourceCommitMatchesSourceHead { get; init; }

    public bool ManifestPublishedCliEntryMatchesPublishedCli { get; init; }

    public bool LocalDistFreshnessSmokeReady { get; init; }

    public IReadOnlyList<string> RequiredSourceCommands { get; init; } = [];

    public IReadOnlyList<string> RequiredDistReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
