namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemFollowUpPlanningGate(RuntimeAgentProblemFollowUpPlanningGateSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem follow-up planning gate",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Planning intake document: {surface.PlanningIntakeDocumentPath}",
            $"Planning gate guide document: {surface.PlanningGateGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Problem alias command entry: {surface.ProblemAliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Planning intake posture: {surface.PlanningIntakePosture}",
            $"Planning intake ready: {surface.PlanningIntakeReady}",
            $"Planning gate ready: {surface.PlanningGateReady}",
            $"Accepted planning items: {surface.AcceptedPlanningItemCount}",
            $"Ready for plan init: {surface.ReadyForPlanInitCount}",
            $"Blocked accepted planning items: {surface.BlockedAcceptedPlanningItemCount}",
            $"Formal planning posture: {surface.FormalPlanningPosture}",
            $"Formal planning state: {surface.FormalPlanningState}",
            $"Formal planning entry command: {surface.FormalPlanningEntryCommand}",
            $"Formal planning entry recommended next action: {surface.FormalPlanningEntryRecommendedNextAction}",
            $"Active planning slot state: {surface.ActivePlanningSlotState}",
            $"Active planning slot can initialize: {surface.ActivePlanningSlotCanInitialize}",
            $"Active planning slot conflict reason: {surface.ActivePlanningSlotConflictReason}",
            $"Active planning slot remediation action: {surface.ActivePlanningSlotRemediationAction}",
            $"Planning slot id: {surface.PlanningSlotId ?? "(none)"}",
            $"Plan handle: {surface.PlanHandle ?? "(none)"}",
            $"Planning card id: {surface.PlanningCardId ?? "(none)"}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Planning gate items:",
        };

        if (surface.PlanningGateItems.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var item in surface.PlanningGateItems)
            {
                lines.Add($"- {item.CandidateId} | intake={item.IntakeStatus} | gate={item.PlanningGateStatus} | actionable={item.Actionable}");
                lines.Add($"  next: {item.NextGovernedCommand}");
                lines.Add($"  plan init: {item.SuggestedPlanInitCommand}");
                lines.Add($"  title: {item.SuggestedTitle}");
                lines.Add($"  intent: {item.SuggestedIntent}");
                lines.Add($"  blocking reason: {(string.IsNullOrWhiteSpace(item.BlockingReason) ? "none" : item.BlockingReason)}");
                lines.Add($"  remediation: {(string.IsNullOrWhiteSpace(item.RemediationAction) ? "none" : item.RemediationAction)}");
                lines.Add($"  decision records: {(item.DecisionRecordIds.Count == 0 ? "none" : string.Join(", ", item.DecisionRecordIds))}");
                lines.Add($"  related problems: {(item.RelatedProblemIds.Count == 0 ? "none" : string.Join(", ", item.RelatedProblemIds))}");
            }
        }

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
