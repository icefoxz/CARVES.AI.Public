namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemFollowUpDecisionRecord(RuntimeAgentProblemFollowUpDecisionRecordSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem follow-up decision record",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Decision plan document: {surface.DecisionPlanDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Problem alias command entry: {surface.ProblemAliasCommandEntry}",
            $"Record command entry: {surface.RecordCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Decision plan id: {surface.DecisionPlanId}",
            $"Decision plan posture: {surface.DecisionPlanPosture}",
            $"Decision plan ready: {surface.DecisionPlanReady}",
            $"Decision required: {surface.DecisionRequired}",
            $"Decision record ready: {surface.DecisionRecordReady}",
            $"Record audit ready: {surface.RecordAuditReady}",
            $"Decision record commit ready: {surface.DecisionRecordCommitReady}",
            $"Required decision candidates: {surface.RequiredDecisionCandidateCount}",
            $"Recorded decision candidates: {surface.RecordedDecisionCandidateCount}",
            $"Missing decision candidates: {surface.MissingDecisionCandidateCount}",
            $"Records: {surface.RecordCount}",
            $"Current-plan records: {surface.CurrentPlanRecordCount}",
            $"Valid current-plan records: {surface.ValidCurrentPlanRecordCount}",
            $"Stale records: {surface.StaleRecordCount}",
            $"Invalid records: {surface.InvalidRecordCount}",
            $"Malformed records: {surface.MalformedRecordCount}",
            $"Conflicting decision candidates: {surface.ConflictingDecisionCandidateCount}",
            $"Dirty decision records: {surface.DirtyDecisionRecordCount}",
            $"Untracked decision records: {surface.UntrackedDecisionRecordCount}",
            $"Uncommitted decision records: {surface.UncommittedDecisionRecordCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required decision candidate ids:",
        };

        lines.AddRange(surface.RequiredDecisionCandidateIds.Count == 0
            ? ["- none"]
            : surface.RequiredDecisionCandidateIds.Select(candidateId => $"- {candidateId}"));
        lines.Add("Recorded decision candidate ids:");
        lines.AddRange(surface.RecordedDecisionCandidateIds.Count == 0
            ? ["- none"]
            : surface.RecordedDecisionCandidateIds.Select(candidateId => $"- {candidateId}"));
        lines.Add("Missing decision candidate ids:");
        lines.AddRange(surface.MissingDecisionCandidateIds.Count == 0
            ? ["- none"]
            : surface.MissingDecisionCandidateIds.Select(candidateId => $"- {candidateId}"));
        lines.Add("Invalid decision record paths:");
        lines.AddRange(surface.InvalidDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.InvalidDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Malformed decision record paths:");
        lines.AddRange(surface.MalformedDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.MalformedDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Conflicting decision candidate ids:");
        lines.AddRange(surface.ConflictingDecisionCandidateIds.Count == 0
            ? ["- none"]
            : surface.ConflictingDecisionCandidateIds.Select(candidateId => $"- {candidateId}"));
        lines.Add("Stale decision record ids:");
        lines.AddRange(surface.StaleDecisionRecordIds.Count == 0
            ? ["- none"]
            : surface.StaleDecisionRecordIds.Select(id => $"- {id}"));
        lines.Add("Dirty decision record paths:");
        lines.AddRange(surface.DirtyDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.DirtyDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Untracked decision record paths:");
        lines.AddRange(surface.UntrackedDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.UntrackedDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Uncommitted decision record paths:");
        lines.AddRange(surface.UncommittedDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.UncommittedDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Decision records:");
        if (surface.Records.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var record in surface.Records)
            {
                lines.Add($"- {record.DecisionRecordId} | decision={record.Decision} | candidates={record.CandidateIds.Count}");
                lines.Add($"  path: {record.RecordPath}");
                lines.Add($"  plan: {record.DecisionPlanId}");
                lines.Add($"  operator: {record.Operator}");
                lines.Add($"  reason: {record.Reason}");
                lines.Add($"  readback: {record.ReadbackCommand}");
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
