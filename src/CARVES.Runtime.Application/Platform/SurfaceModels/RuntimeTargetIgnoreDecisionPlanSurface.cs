namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetIgnoreDecisionPlanSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-ignore-decision-plan.v1";

    public string SurfaceId { get; init; } = "runtime-target-ignore-decision-plan";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string ResiduePolicyDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot ignore-plan";

    public string JsonCommandEntry { get; init; } = "carves pilot ignore-plan --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-target-ignore-decision-plan";

    public string ApiCommandEntry { get; init; } = "carves api runtime-target-ignore-decision-plan";

    public string IgnoreDecisionPlanId { get; init; } = string.Empty;

    public string ResiduePolicyPosture { get; init; } = string.Empty;

    public bool CommitClosureComplete { get; init; }

    public bool ResiduePolicyReady { get; init; }

    public bool ProductProofCanRemainComplete { get; init; }

    public bool IgnoreDecisionPlanReady { get; init; }

    public bool IgnoreDecisionRequired { get; init; }

    public bool CanKeepResidueLocal { get; init; }

    public bool CanApplyIgnoreAfterReview { get; init; }

    public bool GitIgnoreExists { get; init; }

    public int ResiduePathCount { get; init; }

    public int SuggestedIgnoreEntryCount { get; init; }

    public int MissingIgnoreEntryCount { get; init; }

    public int DecisionCandidateCount { get; init; }

    public IReadOnlyList<string> ResiduePaths { get; init; } = [];

    public IReadOnlyList<string> SuggestedIgnoreEntries { get; init; } = [];

    public IReadOnlyList<string> MissingIgnoreEntries { get; init; } = [];

    public IReadOnlyList<string> GitIgnorePatchPreview { get; init; } = [];

    public IReadOnlyList<RuntimeTargetIgnoreDecisionCandidateSurface> DecisionCandidates { get; init; } = [];

    public IReadOnlyList<string> OperatorDecisionChecklist { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeTargetIgnoreDecisionCandidateSurface
{
    public string Entry { get; init; } = string.Empty;

    public bool AlreadyPresentInGitIgnore { get; init; }

    public bool OperatorApprovalRequired { get; init; }

    public string RecommendedDecision { get; init; } = string.Empty;

    public IReadOnlyList<string> DecisionOptions { get; init; } = [];

    public int MatchingPathCount { get; init; }

    public IReadOnlyList<string> MatchingPaths { get; init; } = [];

    public string Reason { get; init; } = string.Empty;
}
