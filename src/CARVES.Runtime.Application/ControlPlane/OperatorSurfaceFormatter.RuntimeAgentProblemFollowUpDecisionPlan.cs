namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemFollowUpDecisionPlan(RuntimeAgentProblemFollowUpDecisionPlanSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem follow-up decision plan",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.DecisionPlanGuideDocumentPath}",
            $"Follow-up candidates guide document: {surface.FollowUpCandidatesGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Triage alias command entry: {surface.TriageAliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Decision plan id: {surface.DecisionPlanId}",
            $"Candidate surface posture: {surface.CandidateSurfacePosture}",
            $"Candidate surface ready: {surface.CandidateSurfaceReady}",
            $"Decision plan ready: {surface.DecisionPlanReady}",
            $"Decision required: {surface.DecisionRequired}",
            $"Recorded problem count: {surface.RecordedProblemCount}",
            $"Candidate count: {surface.CandidateCount}",
            $"Governed candidate count: {surface.GovernedCandidateCount}",
            $"Watchlist candidate count: {surface.WatchlistCandidateCount}",
            $"Decision item count: {surface.DecisionItemCount}",
            $"Operator review item count: {surface.OperatorReviewItemCount}",
            $"Watchlist item count: {surface.WatchlistItemCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Decision items:",
        };

        if (surface.DecisionItems.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var item in surface.DecisionItems)
            {
                lines.Add($"- {item.CandidateId}: recommended_decision={item.RecommendedDecision}; status={item.CandidateStatus}; kind={item.ProblemKind}; lane={item.RecommendedTriageLane}; problems={item.ProblemCount}; blocking={item.BlockingCount}");
                lines.Add($"  planning_entry_hint: {item.PlanningEntryHint}");
                lines.Add($"  options: {string.Join(", ", item.DecisionOptions)}");
                lines.Add($"  suggested_title: {item.SuggestedTitle}");
            }
        }

        lines.Add("Operator decision checklist:");
        lines.AddRange(surface.OperatorDecisionChecklist.Count == 0
            ? ["- none"]
            : surface.OperatorDecisionChecklist.Select(item => $"- {item}"));
        lines.Add("Boundary rules:");
        lines.AddRange(surface.BoundaryRules.Count == 0
            ? ["- none"]
            : surface.BoundaryRules.Select(rule => $"- {rule}"));
        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
