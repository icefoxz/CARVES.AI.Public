namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetResiduePolicySurface
{
    public string SchemaVersion { get; init; } = "runtime-target-residue-policy.v1";

    public string SurfaceId { get; init; } = "runtime-target-residue-policy";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string CommitClosureDocumentPath { get; init; } = string.Empty;

    public string CommitPlanDocumentPath { get; init; } = string.Empty;

    public string CommitHygieneDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot residue";

    public string JsonCommandEntry { get; init; } = "carves pilot residue --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-target-residue-policy";

    public string ApiCommandEntry { get; init; } = "carves api runtime-target-residue-policy";

    public string CommitClosurePosture { get; init; } = string.Empty;

    public string CommitPlanPosture { get; init; } = string.Empty;

    public string CommitPlanId { get; init; } = string.Empty;

    public bool RuntimeInitialized { get; init; }

    public bool GitRepositoryDetected { get; init; }

    public bool TargetGitWorktreeClean { get; init; }

    public bool CommitClosureComplete { get; init; }

    public bool ResiduePolicyReady { get; init; }

    public bool ProductProofCanRemainComplete { get; init; }

    public bool CanKeepResidueLocal { get; init; }

    public bool CanAddIgnoreAfterReview { get; init; }

    public int StagePathCount { get; init; }

    public int ResiduePathCount { get; init; }

    public int OperatorReviewRequiredPathCount { get; init; }

    public int SuggestedIgnoreEntryCount { get; init; }

    public IReadOnlyList<string> StagePaths { get; init; } = [];

    public IReadOnlyList<string> ResiduePaths { get; init; } = [];

    public IReadOnlyList<string> OperatorReviewRequiredPaths { get; init; } = [];

    public IReadOnlyList<string> SuggestedIgnoreEntries { get; init; } = [];

    public IReadOnlyList<RuntimeTargetResidueIgnoreSuggestionSurface> IgnoreSuggestions { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeTargetResidueIgnoreSuggestionSurface
{
    public string Entry { get; init; } = string.Empty;

    public int MatchingPathCount { get; init; }

    public IReadOnlyList<string> MatchingPaths { get; init; } = [];

    public string Reason { get; init; } = string.Empty;
}
