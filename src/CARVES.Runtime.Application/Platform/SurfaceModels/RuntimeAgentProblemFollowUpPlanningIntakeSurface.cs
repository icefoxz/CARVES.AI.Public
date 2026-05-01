namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemFollowUpPlanningIntakeSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-planning-intake.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-follow-up-planning-intake";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath;

    public string DecisionRecordDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath;

    public string DecisionPlanDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpPlanningIntakeService.PlanningIntakeGuideDocumentPath;

    public string DecisionRecordGuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot follow-up-intake";

    public string JsonCommandEntry { get; init; } = "carves pilot follow-up-intake --json";

    public string AliasCommandEntry { get; init; } = "carves pilot follow-up-planning --json";

    public string ProblemAliasCommandEntry { get; init; } = "carves pilot problem-follow-up-intake --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-follow-up-planning-intake";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-follow-up-planning-intake";

    public string DecisionPlanId { get; init; } = string.Empty;

    public string DecisionRecordPosture { get; init; } = string.Empty;

    public bool DecisionRecordReady { get; init; }

    public bool DecisionRecordCommitReady { get; init; }

    public bool RecordAuditReady { get; init; }

    public bool PlanningIntakeReady { get; init; }

    public int AcceptedDecisionRecordCount { get; init; }

    public int RejectedDecisionRecordCount { get; init; }

    public int WaitingDecisionRecordCount { get; init; }

    public int NonActionableDecisionRecordCount { get; init; }

    public int AcceptedPlanningItemCount { get; init; }

    public int ActionablePlanningItemCount { get; init; }

    public int ConsumedPlanningItemCount { get; init; }

    public IReadOnlyList<string> ConsumedPlanningCandidateIds { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemFollowUpPlanningIntakeItemSurface> PlanningItems { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemFollowUpPlanningIntakeDecisionSurface> NonActionableDecisions { get; init; } = [];

    public IReadOnlyList<string> PlanningLaneCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpPlanningIntakeItemSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-planning-intake-item.v1";

    public string CandidateId { get; init; } = string.Empty;

    public string IntakeStatus { get; init; } = string.Empty;

    public bool Actionable { get; init; }

    public string SuggestedTitle { get; init; } = string.Empty;

    public string SuggestedIntent { get; init; } = string.Empty;

    public string SuggestedAcceptanceEvidence { get; init; } = string.Empty;

    public string SuggestedReadbackCommand { get; init; } = string.Empty;

    public string SuggestedIntentDraftCommand { get; init; } = "carves intent draft --persist";

    public string SuggestedPlanInitCommand { get; init; } = "carves plan init [candidate-card-id]";

    public string Decision { get; init; } = string.Empty;

    public IReadOnlyList<string> DecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> DecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> RelatedProblemIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> OperatorReasons { get; init; } = [];

    public IReadOnlyList<string> AcceptanceEvidence { get; init; } = [];

    public IReadOnlyList<string> ReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> PlanningRequirements { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpPlanningIntakeDecisionSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-planning-intake-decision.v1";

    public string DecisionRecordId { get; init; } = string.Empty;

    public string RecordPath { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public string Reason { get; init; } = string.Empty;

    public string IntakeStatus { get; init; } = string.Empty;
}
