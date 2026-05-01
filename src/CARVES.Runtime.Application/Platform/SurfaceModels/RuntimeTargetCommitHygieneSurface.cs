namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetCommitHygieneSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-commit-hygiene.v1";

    public string SurfaceId { get; init; } = "runtime-target-commit-hygiene";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot commit-hygiene";

    public string JsonCommandEntry { get; init; } = "carves pilot commit-hygiene --json";

    public bool RuntimeInitialized { get; init; }

    public bool GitRepositoryDetected { get; init; }

    public bool CanProceedToCommit { get; init; }

    public int DirtyPathCount { get; init; }

    public int CommitCandidatePathCount { get; init; }

    public int OfficialTruthPathCount { get; init; }

    public int TargetOutputCandidatePathCount { get; init; }

    public int LocalResiduePathCount { get; init; }

    public int UnclassifiedPathCount { get; init; }

    public IReadOnlyList<RuntimeTargetCommitHygienePathSurface> DirtyPaths { get; init; } = [];

    public IReadOnlyList<string> CommitCandidatePaths { get; init; } = [];

    public IReadOnlyList<string> ExcludedPaths { get; init; } = [];

    public IReadOnlyList<string> OperatorReviewRequiredPaths { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeTargetCommitHygienePathSurface
{
    public string StatusCode { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string PathClass { get; init; } = string.Empty;

    public string CommitPosture { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;
}
