using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed record PlannerOpportunityPreviewResult(
    string Reason,
    int PlannerRound,
    int DetectedOpportunityCount,
    int EvaluatedOpportunityCount,
    string OpportunitySourceSummary,
    IReadOnlyList<Opportunity> SelectedOpportunities,
    OpportunityTaskPreviewResult Preview,
    PlannerAutonomyLimit AutonomyLimit,
    bool RequiresOperatorPause)
{
    public bool ProducedWork => Preview.ProposedTasks.Count > 0;

    public IReadOnlyList<string> ProposedTaskIds => Preview.ProposedTasks.Select(task => task.TaskId).ToArray();
}
