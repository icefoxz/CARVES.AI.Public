namespace Carves.Runtime.Application.Planning;

public sealed record PlannerOpportunityEvaluationResult(
    string Reason,
    int PlannerRound,
    int DetectedOpportunityCount,
    int EvaluatedOpportunityCount,
    string OpportunitySourceSummary,
    IReadOnlyList<string> MaterializedTaskIds,
    PlannerAutonomyLimit AutonomyLimit,
    bool RequiresOperatorPause)
{
    public bool ProducedWork => MaterializedTaskIds.Count > 0;
}
