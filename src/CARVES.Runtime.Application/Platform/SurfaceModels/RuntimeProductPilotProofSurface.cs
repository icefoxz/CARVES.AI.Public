namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeProductPilotProofSurface
{
    public string SchemaVersion { get; init; } = "runtime-product-pilot-proof.v1";

    public string SurfaceId { get; init; } = "runtime-product-pilot-proof";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string PilotGuideDocumentPath { get; init; } = string.Empty;

    public string PilotStatusDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot proof";

    public string JsonCommandEntry { get; init; } = "carves pilot proof --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-product-pilot-proof";

    public string ApiCommandEntry { get; init; } = "carves api runtime-product-pilot-proof";

    public bool ProductPilotProofComplete { get; init; }

    public string LocalDistFreshnessSmokePosture { get; init; } = string.Empty;

    public bool LocalDistFreshnessSmokeReady { get; init; }

    public string LocalDistFreshnessSmokeSourceCommit { get; init; } = string.Empty;

    public string LocalDistHandoffPosture { get; init; } = string.Empty;

    public bool StableExternalConsumptionReady { get; init; }

    public string RuntimeRootKind { get; init; } = string.Empty;

    public string RuntimeDistManifestVersion { get; init; } = string.Empty;

    public string RuntimeDistManifestSourceCommit { get; init; } = string.Empty;

    public string FrozenDistTargetReadbackProofPosture { get; init; } = string.Empty;

    public bool FrozenDistTargetReadbackProofComplete { get; init; }

    public string TargetCommitClosurePosture { get; init; } = string.Empty;

    public string TargetCommitPlanPosture { get; init; } = string.Empty;

    public string TargetResiduePolicyPosture { get; init; } = string.Empty;

    public string TargetIgnoreDecisionPlanPosture { get; init; } = string.Empty;

    public string TargetIgnoreDecisionRecordPosture { get; init; } = string.Empty;

    public string CommitPlanId { get; init; } = string.Empty;

    public bool RuntimeInitialized { get; init; }

    public bool GitRepositoryDetected { get; init; }

    public bool TargetGitWorktreeClean { get; init; }

    public bool TargetCommitClosureComplete { get; init; }

    public bool TargetResiduePolicyReady { get; init; }

    public bool ProductProofCanRemainCompleteWithResidue { get; init; }

    public bool TargetIgnoreDecisionPlanReady { get; init; }

    public bool TargetIgnoreDecisionRecordReady { get; init; }

    public bool TargetIgnoreDecisionRecordAuditReady { get; init; }

    public bool TargetIgnoreDecisionRecordCommitReady { get; init; }

    public bool IgnoreDecisionRequired { get; init; }

    public bool CanApplyIgnoreAfterReview { get; init; }

    public bool CanStage { get; init; }

    public int StagePathCount { get; init; }

    public int ExcludedPathCount { get; init; }

    public int OperatorReviewRequiredPathCount { get; init; }

    public int SuggestedIgnoreEntryCount { get; init; }

    public int MissingIgnoreEntryCount { get; init; }

    public int RequiredIgnoreDecisionEntryCount { get; init; }

    public int RecordedIgnoreDecisionEntryCount { get; init; }

    public int MissingIgnoreDecisionEntryCount { get; init; }

    public int InvalidIgnoreDecisionRecordCount { get; init; }

    public int MalformedIgnoreDecisionRecordCount { get; init; }

    public int ConflictingIgnoreDecisionEntryCount { get; init; }

    public int UncommittedIgnoreDecisionRecordCount { get; init; }

    public IReadOnlyList<string> StagePaths { get; init; } = [];

    public IReadOnlyList<string> ExcludedPaths { get; init; } = [];

    public IReadOnlyList<string> OperatorReviewRequiredPaths { get; init; } = [];

    public IReadOnlyList<string> SuggestedIgnoreEntries { get; init; } = [];

    public IReadOnlyList<string> MissingIgnoreEntries { get; init; } = [];

    public IReadOnlyList<string> MissingIgnoreDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> IgnoreDecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> InvalidIgnoreDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> MalformedIgnoreDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> ConflictingIgnoreDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> UncommittedIgnoreDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> RequiredReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
