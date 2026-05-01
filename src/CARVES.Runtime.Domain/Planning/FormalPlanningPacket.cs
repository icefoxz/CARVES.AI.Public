namespace Carves.Runtime.Domain.Planning;

public sealed class FormalPlanningPacket
{
    public string PlanHandle { get; init; } = string.Empty;

    public string PlanningSlotId { get; init; } = string.Empty;

    public string PlanningCardId { get; init; } = string.Empty;

    public string SourceIntentDraftId { get; init; } = string.Empty;

    public string? SourceCandidateCardId { get; init; }

    public FormalPlanningState FormalPlanningState { get; init; } = FormalPlanningState.Discuss;

    public FormalPlanningBriefing Briefing { get; init; } = new();

    public FormalPlanningAcceptanceSummary AcceptanceContractSummary { get; init; } = new();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DecompositionCandidates { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidenceExpectations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedScopeSummary { get; init; } = Array.Empty<string>();

    public IReadOnlyList<FormalPlanningReplanRule> ReplanRules { get; init; } = Array.Empty<FormalPlanningReplanRule>();

    public FormalPlanningLinkedTruth LinkedTruth { get; init; } = new();
}

public sealed class FormalPlanningBriefing
{
    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;

    public FormalPlanningNextActionPosture NextActionPosture { get; init; } = FormalPlanningNextActionPosture.DiscussionOnly;

    public bool ReplanRequired { get; init; }
}

public enum FormalPlanningNextActionPosture
{
    DiscussionOnly,
    PlanInitRequired,
    PlanExportRequired,
    CardDraftRequired,
    TaskGraphDraftRequired,
    ExecutionFollowThrough,
    ReviewFollowThrough,
    ClosedObserve,
    ReplanRefreshRequired,
}

public sealed class FormalPlanningAcceptanceSummary
{
    public string BindingState { get; init; } = "not_bound_yet";

    public string? ContractId { get; init; }

    public AcceptanceContractLifecycleStatus? LifecycleStatus { get; init; }

    public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();

    public string GapSummary { get; init; } = string.Empty;
}

public sealed class FormalPlanningReplanRule
{
    public string RuleId { get; init; } = string.Empty;

    public string Trigger { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;

    public string ReentryCommand { get; init; } = string.Empty;
}

public sealed class FormalPlanningLinkedTruth
{
    public IReadOnlyList<string> CardDraftIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TaskGraphDraftIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TaskIds { get; init; } = Array.Empty<string>();
}

public sealed record FormalPlanningPacketExportResult(
    string OutputPath,
    string PlanHandle,
    string PlanningCardId);
