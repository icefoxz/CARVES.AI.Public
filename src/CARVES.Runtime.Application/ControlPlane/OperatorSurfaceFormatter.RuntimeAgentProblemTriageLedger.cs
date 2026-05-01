namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemTriageLedger(RuntimeAgentProblemTriageLedgerSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem triage ledger",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.TriageLedgerGuideDocumentPath}",
            $"Problem intake guide document: {surface.ProblemIntakeGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Friction ledger command entry: {surface.FrictionLedgerCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Problem storage root: {surface.ProblemStorageRoot}",
            $"Evidence ledger root: {surface.EvidenceLedgerRoot}",
            $"Triage ledger ready: {surface.TriageLedgerReady}",
            $"Recorded problem count: {surface.RecordedProblemCount}",
            $"Blocking problem count: {surface.BlockingProblemCount}",
            $"Repo count: {surface.RepoCount}",
            $"Distinct problem kind count: {surface.DistinctProblemKindCount}",
            $"Review queue count: {surface.ReviewQueueCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Problem kind ledger:",
        };

        lines.AddRange(surface.ProblemKindLedger.Count == 0
            ? ["- none"]
            : surface.ProblemKindLedger.Select(item => $"- {item.ProblemKind}: count={item.Count}; blocking={item.BlockingCount}; lane={item.RecommendedTriageLane}; latest={FormatProblemTriageTimestamp(item.LatestRecordedAtUtc)}"));
        lines.Add("Severity ledger:");
        lines.AddRange(surface.SeverityLedger.Count == 0
            ? ["- none"]
            : surface.SeverityLedger.Select(item => $"- {item.Severity}: count={item.Count}"));
        lines.Add("Stage ledger:");
        lines.AddRange(surface.StageLedger.Count == 0
            ? ["- none"]
            : surface.StageLedger.Select(item => $"- {item.CurrentStageId}: count={item.Count}; latest={FormatProblemTriageTimestamp(item.LatestRecordedAtUtc)}"));
        lines.Add("Review queue:");
        lines.AddRange(surface.ReviewQueue.Count == 0
            ? ["- none"]
            : surface.ReviewQueue.Select(item => $"- {item.ProblemId}: [{item.ProblemKind}/{item.Severity}] {item.Summary} | lane={item.RecommendedTriageLane}; evidence={item.EvidenceId}; repo={EmptyProblemTriageValueAsNone(item.RepoId)}; stage={item.CurrentStageId}"));
        lines.Add("Triage rules:");
        lines.AddRange(surface.TriageRules.Count == 0
            ? ["- none"]
            : surface.TriageRules.Select(rule => $"- {rule}"));
        lines.Add("Recommended operator actions:");
        lines.AddRange(surface.RecommendedOperatorActions.Count == 0
            ? ["- none"]
            : surface.RecommendedOperatorActions.Select(action => $"- {action}"));
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

    private static string FormatProblemTriageTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "(none)" : timestamp.Value.ToString("O");
    }

    private static string EmptyProblemTriageValueAsNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }
}
