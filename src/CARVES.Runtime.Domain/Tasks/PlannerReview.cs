namespace Carves.Runtime.Domain.Tasks;

public sealed class PlannerReview
{
    public PlannerVerdict Verdict { get; init; } = PlannerVerdict.Continue;

    public string Reason { get; init; } = string.Empty;

    public ReviewDecisionStatus DecisionStatus { get; init; } = ReviewDecisionStatus.NeedsAttention;

    public bool AcceptanceMet { get; init; }

    public bool BoundaryPreserved { get; init; } = true;

    public bool ScopeDriftDetected { get; init; }

    public IReadOnlyList<string> FollowUpSuggestions { get; init; } = Array.Empty<string>();

    public ReviewDecisionDebt? DecisionDebt { get; init; }
}
