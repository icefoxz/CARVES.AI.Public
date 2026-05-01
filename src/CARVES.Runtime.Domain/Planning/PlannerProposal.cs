namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposal
{
    public string ProposalId { get; init; } = string.Empty;

    public string PlannerBackend { get; init; } = string.Empty;

    public string GoalSummary { get; init; } = string.Empty;

    public PlannerRecommendedAction RecommendedAction { get; init; } = PlannerRecommendedAction.Observe;

    public PlannerSleepReason SleepRecommendation { get; init; } = PlannerSleepReason.None;

    public PlannerEscalationReason EscalationRecommendation { get; init; } = PlannerEscalationReason.None;

    public IReadOnlyList<PlannerProposedCard> ProposedCards { get; init; } = Array.Empty<PlannerProposedCard>();

    public IReadOnlyList<PlannerProposedTask> ProposedTasks { get; init; } = Array.Empty<PlannerProposedTask>();

    public IReadOnlyList<PlannerProposedDependency> Dependencies { get; init; } = Array.Empty<PlannerProposedDependency>();

    public IReadOnlyList<PlannerProposalRiskFlag> RiskFlags { get; init; } = Array.Empty<PlannerProposalRiskFlag>();

    public double Confidence { get; init; }

    public string Rationale { get; init; } = string.Empty;
}
