namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemFollowUpDecisionRecordSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-decision-record.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-follow-up-decision-record";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string DecisionPlanDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot follow-up-record";

    public string JsonCommandEntry { get; init; } = "carves pilot follow-up-record --json";

    public string AliasCommandEntry { get; init; } = "carves pilot follow-up-decision-record --json";

    public string ProblemAliasCommandEntry { get; init; } = "carves pilot problem-follow-up-record --json";

    public string RecordCommandEntry { get; init; } = "carves pilot record-follow-up-decision <decision> --candidate <candidate-id> --reason <text>";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-follow-up-decision-record";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-follow-up-decision-record";

    public string DecisionPlanId { get; init; } = string.Empty;

    public string DecisionPlanPosture { get; init; } = string.Empty;

    public bool DecisionPlanReady { get; init; }

    public bool DecisionRequired { get; init; }

    public bool DecisionRecordReady { get; init; }

    public bool RecordAuditReady { get; init; }

    public bool DecisionRecordCommitReady { get; init; }

    public int RequiredDecisionCandidateCount { get; init; }

    public int RecordedDecisionCandidateCount { get; init; }

    public int MissingDecisionCandidateCount { get; init; }

    public int RecordCount { get; init; }

    public int CurrentPlanRecordCount { get; init; }

    public int ValidCurrentPlanRecordCount { get; init; }

    public int StaleRecordCount { get; init; }

    public int InvalidRecordCount { get; init; }

    public int MalformedRecordCount { get; init; }

    public int ConflictingDecisionCandidateCount { get; init; }

    public int DirtyDecisionRecordCount { get; init; }

    public int UntrackedDecisionRecordCount { get; init; }

    public int UncommittedDecisionRecordCount { get; init; }

    public IReadOnlyList<string> RequiredDecisionCandidateIds { get; init; } = [];

    public IReadOnlyList<string> RecordedDecisionCandidateIds { get; init; } = [];

    public IReadOnlyList<string> MissingDecisionCandidateIds { get; init; } = [];

    public IReadOnlyList<string> DecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> StaleDecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> InvalidDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> MalformedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> ConflictingDecisionCandidateIds { get; init; } = [];

    public IReadOnlyList<string> DirtyDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> UntrackedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> UncommittedDecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> Records { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpDecisionRecordEntrySurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-decision-record-entry.v1";

    public string DecisionRecordId { get; init; } = string.Empty;

    public string RecordPath { get; init; } = string.Empty;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string SourceSurfaceId { get; init; } = "runtime-agent-problem-follow-up-decision-plan";

    public string DecisionPlanId { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public IReadOnlyList<string> RelatedProblemIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];

    public string Reason { get; init; } = string.Empty;

    public string Operator { get; init; } = "operator";

    public string AcceptanceEvidence { get; init; } = string.Empty;

    public string ReadbackCommand { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
