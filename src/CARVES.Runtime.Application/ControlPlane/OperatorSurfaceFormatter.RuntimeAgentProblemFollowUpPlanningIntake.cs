namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemFollowUpPlanningIntake(RuntimeAgentProblemFollowUpPlanningIntakeSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem follow-up planning intake",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Decision record document: {surface.DecisionRecordDocumentPath}",
            $"Decision plan document: {surface.DecisionPlanDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Decision record guide document: {surface.DecisionRecordGuideDocumentPath}",
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
            $"Decision plan id: {surface.DecisionPlanId}",
            $"Decision record posture: {surface.DecisionRecordPosture}",
            $"Decision record ready: {surface.DecisionRecordReady}",
            $"Decision record commit ready: {surface.DecisionRecordCommitReady}",
            $"Record audit ready: {surface.RecordAuditReady}",
            $"Planning intake ready: {surface.PlanningIntakeReady}",
            $"Accepted decision records: {surface.AcceptedDecisionRecordCount}",
            $"Rejected decision records: {surface.RejectedDecisionRecordCount}",
            $"Waiting decision records: {surface.WaitingDecisionRecordCount}",
            $"Non-actionable decision records: {surface.NonActionableDecisionRecordCount}",
            $"Accepted planning items: {surface.AcceptedPlanningItemCount}",
            $"Actionable planning items: {surface.ActionablePlanningItemCount}",
            $"Consumed planning items: {surface.ConsumedPlanningItemCount}",
            $"Consumed planning candidate ids: {(surface.ConsumedPlanningCandidateIds.Count == 0 ? "none" : string.Join(", ", surface.ConsumedPlanningCandidateIds))}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Planning lane commands:",
        };

        lines.AddRange(surface.PlanningLaneCommands.Count == 0
            ? ["- none"]
            : surface.PlanningLaneCommands.Select(command => $"- {command}"));
        lines.Add("Planning items:");
        if (surface.PlanningItems.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var item in surface.PlanningItems)
            {
                lines.Add($"- {item.CandidateId} | status={item.IntakeStatus} | actionable={item.Actionable}");
                lines.Add($"  title: {item.SuggestedTitle}");
                lines.Add($"  intent: {item.SuggestedIntent}");
                lines.Add($"  intent draft: {item.SuggestedIntentDraftCommand}");
                lines.Add($"  plan init: {item.SuggestedPlanInitCommand}");
                lines.Add($"  readback: {item.SuggestedReadbackCommand}");
                lines.Add($"  decision records: {(item.DecisionRecordIds.Count == 0 ? "none" : string.Join(", ", item.DecisionRecordIds))}");
            }
        }

        lines.Add("Non-actionable decisions:");
        if (surface.NonActionableDecisions.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var decision in surface.NonActionableDecisions)
            {
                lines.Add($"- {decision.DecisionRecordId} | decision={decision.Decision} | status={decision.IntakeStatus}");
                lines.Add($"  path: {decision.RecordPath}");
                lines.Add($"  candidates: {(decision.CandidateIds.Count == 0 ? "none" : string.Join(", ", decision.CandidateIds))}");
                lines.Add($"  reason: {decision.Reason}");
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
