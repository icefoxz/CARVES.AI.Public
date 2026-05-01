namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetCommitPlanSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-commit-plan.v1";

    public string SurfaceId { get; init; } = "runtime-target-commit-plan";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string HygieneDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string CommitPlanId { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot commit-plan";

    public string JsonCommandEntry { get; init; } = "carves pilot commit-plan --json";

    public string HygieneCommandEntry { get; init; } = "carves pilot commit-hygiene --json";

    public bool RuntimeInitialized { get; init; }

    public bool GitRepositoryDetected { get; init; }

    public bool CanStage { get; init; }

    public bool CanCommitAfterStaging { get; init; }

    public int StagePathCount { get; init; }

    public int ExcludedPathCount { get; init; }

    public int OperatorReviewRequiredPathCount { get; init; }

    public IReadOnlyList<string> StagePaths { get; init; } = [];

    public IReadOnlyList<string> ExcludedPaths { get; init; } = [];

    public IReadOnlyList<string> OperatorReviewRequiredPaths { get; init; } = [];

    public string SuggestedCommitMessage { get; init; } = string.Empty;

    public string GitAddCommandPreview { get; init; } = string.Empty;

    public string GitCommitCommandPreview { get; init; } = string.Empty;

    public string HygieneSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
