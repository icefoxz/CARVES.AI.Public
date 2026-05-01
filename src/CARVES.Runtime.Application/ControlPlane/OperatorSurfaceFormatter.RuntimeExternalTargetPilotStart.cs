namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeExternalTargetPilotStart(RuntimeExternalTargetPilotStartSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime external target pilot start",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Quickstart guide: {surface.QuickstartGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Next command entry: {surface.NextCommandEntry}",
            $"Next JSON command entry: {surface.NextJsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Pilot start bundle ready: {surface.PilotStartBundleReady}",
            $"Alpha external use ready: {surface.AlphaExternalUseReady}",
            $"Invocation contract complete: {surface.InvocationContractComplete}",
            $"External consumer resource pack complete: {surface.ExternalConsumerResourcePackComplete}",
            $"Governed agent handoff ready: {surface.GovernedAgentHandoffReady}",
            $"Product pilot status valid: {surface.ProductPilotStatusValid}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Target agent bootstrap ready: {surface.TargetAgentBootstrapReady}",
            $"Target ready for formal planning: {surface.TargetReadyForFormalPlanning}",
            $"Recommended invocation mode: {surface.RecommendedInvocationMode}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Candidate dist root: {surface.CandidateDistRoot}",
            $"Current stage: {surface.CurrentStageOrder} {surface.CurrentStageId}",
            $"Current stage status: {surface.CurrentStageStatus}",
            $"Pilot status posture: {surface.PilotStatusPosture}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Legacy next command projection only: {surface.LegacyNextCommandProjectionOnly}",
            $"Legacy next command do not auto-run: {surface.LegacyNextCommandDoNotAutoRun}",
            $"Preferred action source: {surface.PreferredActionSource}",
            $"Discussion-first surface: {surface.DiscussionFirstSurface}",
            $"Auto-run allowed: {surface.AutoRunAllowed}",
            $"Recommended action id: {surface.RecommendedActionId ?? "(none)"}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Available actions:",
        };

        lines.AddRange(surface.AvailableActions.Count == 0
            ? ["- none"]
            : surface.AvailableActions.Select(action => $"- {action.ActionId} | {action.Kind} | {action.Label} | {action.Command}"));
        lines.Add("Forbidden auto actions:");
        lines.AddRange(surface.ForbiddenAutoActions.Count == 0
            ? ["- none"]
            : surface.ForbiddenAutoActions.Select(action => $"- {action}"));
        lines.AddRange(
        [
            "Start readback commands:",
        ]);

        lines.AddRange(surface.StartReadbackCommands.Count == 0
            ? ["- none"]
            : surface.StartReadbackCommands.Select(command => $"- {command}"));
        lines.Add("Agent operating rules:");
        lines.AddRange(surface.AgentOperatingRules.Count == 0
            ? ["- none"]
            : surface.AgentOperatingRules.Select(rule => $"- {rule}"));
        lines.Add("Stop and report triggers:");
        lines.AddRange(surface.StopAndReportTriggers.Count == 0
            ? ["- none"]
            : surface.StopAndReportTriggers.Select(trigger => $"- {trigger}"));
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

    public static OperatorCommandResult RuntimeExternalTargetPilotNext(RuntimeExternalTargetPilotNextSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime external target pilot next",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Quickstart guide: {surface.QuickstartGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Ready to run next command: {surface.ReadyToRunNextCommand}",
            $"Alpha external use ready: {surface.AlphaExternalUseReady}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Current stage: {surface.CurrentStageOrder} {surface.CurrentStageId}",
            $"Current stage status: {surface.CurrentStageStatus}",
            $"Pilot status posture: {surface.PilotStatusPosture}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Legacy next command projection only: {surface.LegacyNextCommandProjectionOnly}",
            $"Legacy next command do not auto-run: {surface.LegacyNextCommandDoNotAutoRun}",
            $"Preferred action source: {surface.PreferredActionSource}",
            $"Discussion-first surface: {surface.DiscussionFirstSurface}",
            $"Auto-run allowed: {surface.AutoRunAllowed}",
            $"Recommended action id: {surface.RecommendedActionId ?? "(none)"}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Available actions:",
        };

        lines.AddRange(surface.AvailableActions.Count == 0
            ? ["- none"]
            : surface.AvailableActions.Select(action => $"- {action.ActionId} | {action.Kind} | {action.Label} | {action.Command}"));
        lines.Add("Forbidden auto actions:");
        lines.AddRange(surface.ForbiddenAutoActions.Count == 0
            ? ["- none"]
            : surface.ForbiddenAutoActions.Select(action => $"- {action}"));
        lines.AddRange(
        [
            "Stop and report triggers:",
        ]);

        lines.AddRange(surface.StopAndReportTriggers.Count == 0
            ? ["- none"]
            : surface.StopAndReportTriggers.Select(trigger => $"- {trigger}"));
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
