namespace Carves.Runtime.Application.Planning;

public sealed record PlannerReentryResult(
    PlannerReentryOutcome Outcome,
    string Reason,
    IReadOnlyList<string> ProposedTaskIds,
    bool RequiresOperatorPause,
    int PlannerRound = 0,
    int DetectedOpportunityCount = 0,
    int EvaluatedOpportunityCount = 0,
    string OpportunitySourceSummary = "(none)",
    PlannerAutonomyLimit AutonomyLimit = PlannerAutonomyLimit.None)
{
    public bool ProducedWork => ProposedTaskIds.Count > 0;

    public string Message => ProducedWork
        ? $"Planner re-entry {Outcome}: {Reason} Proposed task(s): {string.Join(", ", ProposedTaskIds)}."
        : $"Planner re-entry {Outcome}: {Reason}";
}
