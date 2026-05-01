namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemFollowUpPlanningGateSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-planning-gate.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-follow-up-planning-gate";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string PlanningIntakeDocumentPath { get; init; } = RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath;

    public string PlanningGateGuideDocumentPath { get; init; } = RuntimeAgentProblemFollowUpPlanningGateService.PlanningGateGuideDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot follow-up-gate";

    public string JsonCommandEntry { get; init; } = "carves pilot follow-up-gate --json";

    public string AliasCommandEntry { get; init; } = "carves pilot follow-up-planning-gate --json";

    public string ProblemAliasCommandEntry { get; init; } = "carves pilot problem-follow-up-gate --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-follow-up-planning-gate";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-follow-up-planning-gate";

    public string PlanningIntakePosture { get; init; } = string.Empty;

    public bool PlanningIntakeReady { get; init; }

    public bool PlanningGateReady { get; init; }

    public int AcceptedPlanningItemCount { get; init; }

    public int ReadyForPlanInitCount { get; init; }

    public int BlockedAcceptedPlanningItemCount { get; init; }

    public string FormalPlanningPosture { get; init; } = string.Empty;

    public string FormalPlanningState { get; init; } = string.Empty;

    public string FormalPlanningEntryCommand { get; init; } = "plan init [candidate-card-id]";

    public string FormalPlanningEntryRecommendedNextAction { get; init; } = string.Empty;

    public string ActivePlanningSlotState { get; init; } = string.Empty;

    public bool ActivePlanningSlotCanInitialize { get; init; }

    public string ActivePlanningSlotConflictReason { get; init; } = string.Empty;

    public string ActivePlanningSlotRemediationAction { get; init; } = string.Empty;

    public string? PlanningSlotId { get; init; }

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public string NextGovernedCommand { get; init; } = "carves pilot follow-up-gate --json";

    public IReadOnlyList<RuntimeAgentProblemFollowUpPlanningGateItemSurface> PlanningGateItems { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpPlanningGateItemSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-planning-gate-item.v1";

    public string CandidateId { get; init; } = string.Empty;

    public string IntakeStatus { get; init; } = string.Empty;

    public string PlanningGateStatus { get; init; } = string.Empty;

    public bool Actionable { get; init; }

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string SuggestedPlanInitCommand { get; init; } = string.Empty;

    public string BlockingReason { get; init; } = string.Empty;

    public string RemediationAction { get; init; } = string.Empty;

    public string SuggestedTitle { get; init; } = string.Empty;

    public string SuggestedIntent { get; init; } = string.Empty;

    public IReadOnlyList<string> DecisionRecordIds { get; init; } = [];

    public IReadOnlyList<string> DecisionRecordPaths { get; init; } = [];

    public IReadOnlyList<string> RelatedProblemIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> AcceptanceEvidence { get; init; } = [];

    public IReadOnlyList<string> ReadbackCommands { get; init; } = [];
}
