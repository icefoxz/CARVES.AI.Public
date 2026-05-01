namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeFormalPlanningPosture(RuntimeFormalPlanningPostureSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime formal planning posture",
            $"Plan-mode document: {surface.PlanModeDocumentPath}",
            $"Planning packet document: {surface.PlanningPacketDocumentPath}",
            $"Planning gate document: {surface.PlanningGateDocumentPath}",
            $"Managed workspace document: {surface.ManagedWorkspaceDocumentPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Intent state: {surface.IntentState}",
            $"Guided planning posture: {surface.GuidedPlanningPosture ?? "(none)"}",
            $"Formal planning state: {surface.FormalPlanningState}",
            $"Formal planning entry trigger: {surface.FormalPlanningEntryTriggerState}",
            $"Formal planning entry command: {surface.FormalPlanningEntryCommand}",
            $"Formal planning entry next action: {surface.FormalPlanningEntryRecommendedNextAction}",
            $"Formal planning entry summary: {surface.FormalPlanningEntrySummary}",
            $"Active planning slot state: {surface.ActivePlanningSlotState}",
            $"Active planning slot can initialize: {surface.ActivePlanningSlotCanInitialize}",
            $"Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(surface.ActivePlanningSlotConflictReason) ? "(none)" : surface.ActivePlanningSlotConflictReason)}",
            $"Active planning slot remediation: {surface.ActivePlanningSlotRemediationAction}",
            $"Planning card invariant state: {surface.PlanningCardInvariantState}",
            $"Planning card invariant can export: {surface.PlanningCardInvariantCanExportGovernedTruth}",
            $"Planning card invariant blocks: {surface.PlanningCardInvariantBlockCount}",
            $"Planning card invariant violations: {surface.PlanningCardInvariantViolationCount}",
            $"Planning card invariant remediation: {surface.PlanningCardInvariantRemediationAction}",
            $"Active planning card fill state: {surface.ActivePlanningCardFillState}",
            $"Active planning card fill completion: {surface.ActivePlanningCardFillCompletionPosture}",
            $"Active planning card fill missing required fields: {surface.ActivePlanningCardFillMissingRequiredFieldCount}",
            $"Active planning card fill next missing field: {surface.ActivePlanningCardFillNextMissingFieldPath ?? "(none)"}",
            $"Active planning card fill next action: {surface.ActivePlanningCardFillRecommendedNextAction}",
            $"Current mode: {surface.CurrentMode}",
            $"Planning coupling posture: {surface.PlanningCouplingPosture}",
            $"Planning coupling summary: {surface.PlanningCouplingSummary}",
            $"Planning slot: {surface.PlanningSlotId ?? "(none)"}",
            $"Plan handle: {surface.PlanHandle ?? "(none)"}",
            $"Planning card: {surface.PlanningCardId ?? "(none)"}",
            $"Packet available: {surface.PacketAvailable}",
            $"Packet summary: {surface.PacketSummary ?? "(none)"}",
            $"Recommended next action: {surface.RecommendedNextAction ?? "(none)"}",
            $"Rationale: {surface.Rationale ?? "(none)"}",
            $"Next action posture: {surface.NextActionPosture ?? "(none)"}",
            $"Replan required: {surface.ReplanRequired}",
            $"Managed workspace posture: {surface.ManagedWorkspacePosture}",
            $"Path policy enforcement: {surface.PathPolicyEnforcementState}",
            $"Active leases: {surface.ActiveLeaseCount}",
            $"Active lease tasks: {(surface.ActiveLeaseTaskIds.Count == 0 ? "(none)" : string.Join(", ", surface.ActiveLeaseTaskIds))}",
            $"Dispatch state: {surface.DispatchState}",
            $"Acceptance contract gaps: {surface.AcceptanceContractGapCount}",
            $"Plan-required block count: {surface.PlanRequiredBlockCount}",
            $"Workspace-required block count: {surface.WorkspaceRequiredBlockCount}",
            $"Mode C/D entry first blocked task: {surface.ModeExecutionEntryFirstBlockedTaskId ?? "(none)"}",
            $"Mode C/D entry first blocker: {surface.ModeExecutionEntryFirstBlockingCheckId ?? "(none)"}",
            $"Mode C/D entry first blocker summary: {surface.ModeExecutionEntryFirstBlockingCheckSummary ?? "(none)"}",
            $"Mode C/D entry first blocker action: {surface.ModeExecutionEntryFirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode C/D entry first blocker command: {surface.ModeExecutionEntryFirstBlockingCheckRequiredCommand ?? "(none)"}",
            $"Mode C/D entry next action: {surface.ModeExecutionEntryRecommendedNextAction ?? "(none)"}",
            $"Mode C/D entry next command: {surface.ModeExecutionEntryRecommendedNextCommand ?? "(none)"}",
            $"Missing prerequisites: {surface.MissingPrerequisites.Count}",
        };

        lines.AddRange(surface.MissingPrerequisites.Select(item => $"- prerequisite: {item}"));
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
