namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemIntake(RuntimeAgentProblemIntakeSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem intake",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.ProblemIntakeGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Report problem command entry: {surface.ReportProblemCommandEntry}",
            $"Report problem JSON command entry: {surface.ReportProblemJsonCommandEntry}",
            $"List problems command entry: {surface.ListProblemsCommandEntry}",
            $"Inspect problem command entry: {surface.InspectProblemCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Problem intake ready: {surface.ProblemIntakeReady}",
            $"Pilot start bundle ready: {surface.PilotStartBundleReady}",
            $"Ready to run next command: {surface.ReadyToRunNextCommand}",
            $"Current stage: {surface.CurrentStageOrder} {surface.CurrentStageId}",
            $"Current stage status: {surface.CurrentStageStatus}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Problem storage root: {surface.ProblemStorageRoot}",
            $"Evidence ledger root: {surface.EvidenceLedgerRoot}",
            $"Recent problem count: {surface.RecentProblemCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Accepted problem kinds:",
        };

        lines.AddRange(surface.AcceptedProblemKinds.Count == 0
            ? ["- none"]
            : surface.AcceptedProblemKinds.Select(kind => $"- {kind}"));
        lines.Add("Required payload fields:");
        lines.AddRange(surface.RequiredPayloadFields.Count == 0
            ? ["- none"]
            : surface.RequiredPayloadFields.Select(field => $"- {field}"));
        lines.Add("Optional payload fields:");
        lines.AddRange(surface.OptionalPayloadFields.Count == 0
            ? ["- none"]
            : surface.OptionalPayloadFields.Select(field => $"- {field}"));
        lines.Add("Payload rules:");
        lines.AddRange(surface.PayloadRules.Count == 0
            ? ["- none"]
            : surface.PayloadRules.Select(rule => $"- {rule}"));
        lines.Add("Stop and report triggers:");
        lines.AddRange(surface.StopAndReportTriggers.Count == 0
            ? ["- none"]
            : surface.StopAndReportTriggers.Select(trigger => $"- {trigger}"));
        lines.Add("Command examples:");
        lines.AddRange(surface.CommandExamples.Count == 0
            ? ["- none"]
            : surface.CommandExamples.Select(example => $"- {example}"));
        lines.Add("Recent problems:");
        lines.AddRange(surface.RecentProblems.Count == 0
            ? ["- none"]
            : surface.RecentProblems.Select(problem => $"- {problem.ProblemId}: [{problem.ProblemKind}/{problem.Severity}] {problem.Summary} | evidence={problem.EvidenceId}"));
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
