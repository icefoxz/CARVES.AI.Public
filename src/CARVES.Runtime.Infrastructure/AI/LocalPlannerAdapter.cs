using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class LocalPlannerAdapter : IPlannerAdapter
{
    public string AdapterId => nameof(LocalPlannerAdapter);

    public string ProviderId => "local";

    public string? ProfileId => "local-planner-governed";

    public bool IsConfigured => true;

    public bool IsRealAdapter => false;

    public string SelectionReason { get; }

    public LocalPlannerAdapter(string selectionReason)
    {
        SelectionReason = selectionReason;
    }

    public PlannerProposalEnvelope Run(PlannerRunRequest request)
    {
        var proposedTasks = request.PreviewTasks;
        var dependencies = request.PreviewDependencies;

        var proposal = new PlannerProposal
        {
            ProposalId = request.ProposalId,
            PlannerBackend = "local_preview",
            GoalSummary = request.GoalSummary,
            RecommendedAction = proposedTasks.Count == 0 ? PlannerRecommendedAction.Sleep : PlannerRecommendedAction.ProposeWork,
            SleepRecommendation = proposedTasks.Count == 0 ? PlannerSleepReason.NoOpenOpportunities : PlannerSleepReason.ExistingGovernedWork,
            ProposedTasks = proposedTasks,
            Dependencies = dependencies,
            Confidence = AverageConfidence(request.SelectedOpportunities),
            Rationale = proposedTasks.Count == 0
                ? "No selected opportunities justified governed follow-up work."
                : $"Prepared {proposedTasks.Count} governed tasks from {request.SelectedOpportunities.Count} selected opportunities.",
        };

        return new PlannerProposalEnvelope
        {
            ProposalId = request.ProposalId,
            AdapterId = AdapterId,
            ProviderId = ProviderId,
            ProfileId = ProfileId,
            Configured = true,
            UsedFallback = false,
            WakeReason = request.WakeReason,
            WakeDetail = request.WakeDetail,
            Proposal = proposal,
            RawResponsePreview = proposal.Rationale,
        };
    }

    private static double AverageConfidence(IReadOnlyList<Opportunity> opportunities)
    {
        return opportunities.Count == 0 ? 0 : opportunities.Average(item => item.Confidence);
    }
}
