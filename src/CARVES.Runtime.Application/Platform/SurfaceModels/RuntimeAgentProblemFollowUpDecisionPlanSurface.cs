namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemFollowUpDecisionPlanSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-decision-plan.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-follow-up-decision-plan";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentGuideDocumentPath;

    public string FollowUpCandidatesGuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath;

    public string DecisionPlanGuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot follow-up-plan";

    public string JsonCommandEntry { get; init; } = "carves pilot follow-up-plan --json";

    public string AliasCommandEntry { get; init; } = "carves pilot problem-follow-up-plan --json";

    public string TriageAliasCommandEntry { get; init; } = "carves pilot triage-follow-up-plan --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-follow-up-decision-plan";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-follow-up-decision-plan";

    public string DecisionPlanId { get; init; } = string.Empty;

    public string CandidateSurfacePosture { get; init; } = string.Empty;

    public bool CandidateSurfaceReady { get; init; }

    public bool DecisionPlanReady { get; init; }

    public bool DecisionRequired { get; init; }

    public int RecordedProblemCount { get; init; }

    public int CandidateCount { get; init; }

    public int GovernedCandidateCount { get; init; }

    public int WatchlistCandidateCount { get; init; }

    public int DecisionItemCount { get; init; }

    public int OperatorReviewItemCount { get; init; }

    public int WatchlistItemCount { get; init; }

    public IReadOnlyList<RuntimeAgentProblemFollowUpDecisionItemSurface> DecisionItems { get; init; } = [];

    public IReadOnlyList<string> OperatorDecisionChecklist { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpDecisionItemSurface
{
    public string CandidateId { get; init; } = string.Empty;

    public string CandidateStatus { get; init; } = string.Empty;

    public string ProblemKind { get; init; } = string.Empty;

    public string RecommendedTriageLane { get; init; } = string.Empty;

    public int ProblemCount { get; init; }

    public int BlockingCount { get; init; }

    public string RecommendedDecision { get; init; } = string.Empty;

    public IReadOnlyList<string> DecisionOptions { get; init; } = [];

    public string PlanningEntryHint { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredAcceptanceEvidence { get; init; } = [];

    public string SuggestedTitle { get; init; } = string.Empty;

    public string SuggestedIntent { get; init; } = string.Empty;

    public IReadOnlyList<string> RelatedProblemIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];
}
