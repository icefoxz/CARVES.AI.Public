namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentThreadStart(RuntimeAgentThreadStartSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent thread start",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Startup entry source: {surface.StartupEntrySource}",
            $"Target project classification: {surface.TargetProjectClassification}",
            $"Target classification owner: {surface.TargetClassificationOwner}",
            $"Target classification source: {surface.TargetClassificationSource}",
            $"Agent target classification allowed: {surface.AgentTargetClassificationAllowed}",
            $"Target startup mode: {surface.TargetStartupMode}",
            $"Existing project handling: {surface.ExistingProjectHandling}",
            $"Startup boundary ready: {surface.StartupBoundaryReady}",
            $"Startup boundary posture: {surface.StartupBoundaryPosture}",
            $"Target bound Runtime root: {surface.TargetBoundRuntimeRoot ?? "(none)"}",
            $"Target Runtime binding status: {surface.TargetRuntimeBindingStatus}",
            $"Target Runtime binding source: {surface.TargetRuntimeBindingSource}",
            $"Agent Runtime rebind allowed: {surface.AgentRuntimeRebindAllowed}",
            $"Runtime binding rule: {surface.RuntimeBindingRule}",
            $"Worker execution boundary: {surface.WorkerExecutionBoundary}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Pilot alias command entry: {surface.PilotAliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Thread start ready: {surface.ThreadStartReady}",
            $"One command for new thread: {surface.OneCommandForNewThread}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Next command source: {surface.NextCommandSource}",
            $"Legacy next command projection only: {surface.LegacyNextCommandProjectionOnly}",
            $"Legacy next command do not auto-run: {surface.LegacyNextCommandDoNotAutoRun}",
            $"Preferred action source: {surface.PreferredActionSource}",
            $"Discussion-first surface: {surface.DiscussionFirstSurface}",
            $"Auto-run allowed: {surface.AutoRunAllowed}",
            $"Recommended action id: {surface.RecommendedActionId ?? "(none)"}",
            $"Pilot start posture: {surface.PilotStartPosture}",
            $"Pilot start bundle ready: {surface.PilotStartBundleReady}",
            $"Pilot status posture: {surface.PilotStatusPosture}",
            $"Current stage: {surface.CurrentStageOrder} {surface.CurrentStageId}",
            $"Current stage status: {surface.CurrentStageStatus}",
            $"Pilot status next command: {surface.PilotStatusNextCommand}",
            $"Follow-up gate posture: {surface.FollowUpGatePosture}",
            $"Follow-up gate ready: {surface.FollowUpGateReady}",
            $"Accepted planning item count: {surface.AcceptedPlanningItemCount}",
            $"Ready for plan init count: {surface.ReadyForPlanInitCount}",
            $"Follow-up gate next command: {surface.FollowUpGateNextCommand}",
            $"Handoff posture: {surface.HandoffPosture}",
            $"Governed agent handoff ready: {surface.GovernedAgentHandoffReady}",
            $"Working mode recommendation posture: {surface.WorkingModeRecommendationPosture}",
            $"Protected truth root posture: {surface.ProtectedTruthRootPosture}",
            $"Adapter contract posture: {surface.AdapterContractPosture}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Startup boundary gaps:",
        };

        lines.AddRange(surface.StartupBoundaryGaps.Count == 0
            ? ["- none"]
            : surface.StartupBoundaryGaps.Select(gap => $"- {gap}"));
        lines.Add("Available actions:");
        lines.AddRange(surface.AvailableActions.Count == 0
            ? ["- none"]
            : surface.AvailableActions.Select(action => $"- {action.ActionId} | {action.Kind} | {action.Label} | {action.Command}"));
        lines.Add("Forbidden auto actions:");
        lines.AddRange(surface.ForbiddenAutoActions.Count == 0
            ? ["- none"]
            : surface.ForbiddenAutoActions.Select(action => $"- {action}"));
        lines.AddRange(
        [
            "Minimal agent rules:",
        ]);

        lines.AddRange(surface.MinimalAgentRules.Count == 0
            ? ["- none"]
            : surface.MinimalAgentRules.Select(rule => $"- {rule}"));
        lines.Add("Stop and report triggers:");
        lines.AddRange(surface.StopAndReportTriggers.Count == 0
            ? ["- none"]
            : surface.StopAndReportTriggers.Select(trigger => $"- {trigger}"));
        lines.Add("Troubleshooting readbacks:");
        lines.AddRange(surface.TroubleshootingReadbacks.Count == 0
            ? ["- none"]
            : surface.TroubleshootingReadbacks.Select(command => $"- {command}"));
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
