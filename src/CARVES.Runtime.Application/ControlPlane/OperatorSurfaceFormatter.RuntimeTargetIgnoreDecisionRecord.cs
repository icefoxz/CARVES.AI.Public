namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetIgnoreDecisionRecord(RuntimeTargetIgnoreDecisionRecordSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target ignore decision record",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Ignore decision plan document: {surface.IgnoreDecisionPlanDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Record command entry: {surface.RecordCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Ignore decision plan id: {surface.IgnoreDecisionPlanId}",
            $"Ignore decision plan posture: {surface.IgnoreDecisionPlanPosture}",
            $"Ignore decision plan ready: {surface.IgnoreDecisionPlanReady}",
            $"Ignore decision required: {surface.IgnoreDecisionRequired}",
            $"Decision record ready: {surface.DecisionRecordReady}",
            $"Record audit ready: {surface.RecordAuditReady}",
            $"Decision record commit ready: {surface.DecisionRecordCommitReady}",
            $"Product proof can remain complete: {surface.ProductProofCanRemainComplete}",
            $"Required decision entries: {surface.RequiredDecisionEntryCount}",
            $"Recorded decision entries: {surface.RecordedDecisionEntryCount}",
            $"Missing decision entries: {surface.MissingDecisionEntryCount}",
            $"Records: {surface.RecordCount}",
            $"Current-plan records: {surface.CurrentPlanRecordCount}",
            $"Valid current-plan records: {surface.ValidCurrentPlanRecordCount}",
            $"Stale records: {surface.StaleRecordCount}",
            $"Invalid records: {surface.InvalidRecordCount}",
            $"Malformed records: {surface.MalformedRecordCount}",
            $"Conflicting decision entries: {surface.ConflictingDecisionEntryCount}",
            $"Dirty decision records: {surface.DirtyDecisionRecordCount}",
            $"Untracked decision records: {surface.UntrackedDecisionRecordCount}",
            $"Uncommitted decision records: {surface.UncommittedDecisionRecordCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required decision entry list:",
        };

        lines.AddRange(surface.RequiredDecisionEntries.Count == 0
            ? ["- none"]
            : surface.RequiredDecisionEntries.Select(entry => $"- {entry}"));
        lines.Add("Recorded decision entry list:");
        lines.AddRange(surface.RecordedDecisionEntries.Count == 0
            ? ["- none"]
            : surface.RecordedDecisionEntries.Select(entry => $"- {entry}"));
        lines.Add("Missing decision entry list:");
        lines.AddRange(surface.MissingDecisionEntries.Count == 0
            ? ["- none"]
            : surface.MissingDecisionEntries.Select(entry => $"- {entry}"));
        lines.Add("Invalid decision record paths:");
        lines.AddRange(surface.InvalidDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.InvalidDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Malformed decision record paths:");
        lines.AddRange(surface.MalformedDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.MalformedDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Conflicting decision entry list:");
        lines.AddRange(surface.ConflictingDecisionEntries.Count == 0
            ? ["- none"]
            : surface.ConflictingDecisionEntries.Select(entry => $"- {entry}"));
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
                lines.Add($"- {record.DecisionRecordId} | decision={record.Decision} | entries={record.Entries.Count}");
                lines.Add($"  path: {record.RecordPath}");
                lines.Add($"  plan: {record.IgnoreDecisionPlanId}");
                lines.Add($"  operator: {record.Operator}");
                lines.Add($"  reason: {record.Reason}");
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
