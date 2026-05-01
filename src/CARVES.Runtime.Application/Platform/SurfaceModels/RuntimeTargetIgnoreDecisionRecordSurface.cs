namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetIgnoreDecisionRecordSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-ignore-decision-record.v1";

    public string SurfaceId { get; init; } = "runtime-target-ignore-decision-record";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string IgnoreDecisionPlanDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot ignore-record";

    public string JsonCommandEntry { get; init; } = "carves pilot ignore-record --json";

    public string RecordCommandEntry { get; init; } = "carves pilot record-ignore-decision <decision> --all --reason <text>";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-target-ignore-decision-record";

    public string ApiCommandEntry { get; init; } = "carves api runtime-target-ignore-decision-record";

    public string IgnoreDecisionPlanId { get; init; } = string.Empty;

    public string IgnoreDecisionPlanPosture { get; init; } = string.Empty;

    public bool IgnoreDecisionPlanReady { get; init; }

    public bool IgnoreDecisionRequired { get; init; }

    public bool DecisionRecordReady { get; init; }

    public bool RecordAuditReady { get; init; }

    public bool DecisionRecordCommitReady { get; init; }

    public bool ProductProofCanRemainComplete { get; init; }

    public int RequiredDecisionEntryCount { get; init; }

    public int RecordedDecisionEntryCount { get; init; }

    public int MissingDecisionEntryCount { get; init; }

    public int RecordCount { get; init; }

    public int CurrentPlanRecordCount { get; init; }

    public int ValidCurrentPlanRecordCount { get; init; }

    public int StaleRecordCount { get; init; }

    public int InvalidRecordCount { get; init; }

    public int MalformedRecordCount { get; init; }

    public int ConflictingDecisionEntryCount { get; init; }

    public int DirtyDecisionRecordCount { get; init; }

    public int UntrackedDecisionRecordCount { get; init; }

    public int UncommittedDecisionRecordCount { get; init; }

    public IReadOnlyList<string> RequiredDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> RecordedDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> MissingDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> DecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> StaleDecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> InvalidDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> MalformedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> ConflictingDecisionEntries { get; init; } = [];

    public IReadOnlyList<string> DirtyDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> UntrackedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> UncommittedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<RuntimeTargetIgnoreDecisionRecordEntrySurface> Records { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeTargetIgnoreDecisionRecordEntrySurface
{
    public string SchemaVersion { get; init; } = "runtime-target-ignore-decision-record-entry.v1";

    public string DecisionRecordId { get; init; } = string.Empty;

    public string RecordPath { get; init; } = string.Empty;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string SourceSurfaceId { get; init; } = "runtime-target-ignore-decision-plan";

    public string IgnoreDecisionPlanId { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public IReadOnlyList<string> Entries { get; init; } = [];

    public string Reason { get; init; } = string.Empty;

    public string Operator { get; init; } = "operator";

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
