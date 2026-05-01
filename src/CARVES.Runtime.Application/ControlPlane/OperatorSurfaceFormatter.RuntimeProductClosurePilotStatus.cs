namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeProductClosurePilotStatus(RuntimeProductClosurePilotStatusSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime product closure pilot status",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Operational state: {surface.OperationalState}",
            $"Safe to start new execution: {surface.SafeToStartNewExecution}",
            $"Safe to discuss: {surface.SafeToDiscuss}",
            $"Safe to cleanup: {surface.SafeToCleanup}",
            $"Current stage: {surface.CurrentStageOrder} {surface.CurrentStageId}",
            $"Current stage status: {surface.CurrentStageStatus}",
            $"Next command: {surface.NextCommand}",
            $"Legacy next command projection only: {surface.LegacyNextCommandProjectionOnly}",
            $"Legacy next command do not auto-run: {surface.LegacyNextCommandDoNotAutoRun}",
            $"Preferred action source: {surface.PreferredActionSource}",
            $"Discussion-first surface: {surface.DiscussionFirstSurface}",
            $"Auto-run allowed: {surface.AutoRunAllowed}",
            $"Recommended action id: {surface.RecommendedActionId ?? "(none)"}",
            $"Summary: {surface.Summary}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Target agent bootstrap posture: {surface.TargetAgentBootstrapPosture}",
            $"Target agent bootstrap next action: {surface.TargetAgentBootstrapRecommendedNextAction}",
            $"Target agent bootstrap missing files: {surface.TargetAgentBootstrapMissingFiles.Count}",
            $"Formal planning state: {surface.FormalPlanningState}",
            $"Formal planning posture: {surface.FormalPlanningPosture}",
            $"Managed workspace posture: {surface.ManagedWorkspacePosture}",
            $"Active leases: {surface.ActiveLeaseCount}",
            $"Target commit closure posture: {surface.TargetCommitClosurePosture}",
            $"Target commit closure next action: {surface.TargetCommitClosureRecommendedNextAction}",
            $"Target git worktree clean: {surface.TargetGitWorktreeClean}",
            $"Target commit closure complete: {surface.TargetCommitClosureComplete}",
            $"Target residue policy posture: {surface.TargetResiduePolicyPosture}",
            $"Target residue policy ready: {surface.TargetResiduePolicyReady}",
            $"Target ignore decision plan posture: {surface.TargetIgnoreDecisionPlanPosture}",
            $"Target ignore decision plan ready: {surface.TargetIgnoreDecisionPlanReady}",
            $"Ignore decision required: {surface.IgnoreDecisionRequired}",
            $"Target ignore decision record posture: {surface.TargetIgnoreDecisionRecordPosture}",
            $"Target ignore decision record ready: {surface.TargetIgnoreDecisionRecordReady}",
            $"Target ignore decision record audit ready: {surface.TargetIgnoreDecisionRecordAuditReady}",
            $"Target ignore decision record commit ready: {surface.TargetIgnoreDecisionRecordCommitReady}",
            $"Missing ignore decision entries: {surface.MissingIgnoreDecisionEntryCount}",
            $"Invalid ignore decision records: {surface.InvalidIgnoreDecisionRecordCount}",
            $"Malformed ignore decision records: {surface.MalformedIgnoreDecisionRecordCount}",
            $"Conflicting ignore decision entries: {surface.ConflictingIgnoreDecisionEntryCount}",
            $"Uncommitted ignore decision records: {surface.UncommittedIgnoreDecisionRecordCount}",
            $"Local dist freshness smoke posture: {surface.LocalDistFreshnessSmokePosture}",
            $"Local dist freshness smoke next action: {surface.LocalDistFreshnessSmokeRecommendedNextAction}",
            $"Local dist freshness smoke ready: {surface.LocalDistFreshnessSmokeReady}",
            $"Target dist binding plan posture: {surface.TargetDistBindingPlanPosture}",
            $"Target dist binding plan next action: {surface.TargetDistBindingPlanRecommendedNextAction}",
            $"Local dist handoff posture: {surface.LocalDistHandoffPosture}",
            $"Local dist handoff next action: {surface.LocalDistHandoffRecommendedNextAction}",
            $"Stable external consumption ready: {surface.StableExternalConsumptionReady}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Frozen dist target readback proof posture: {surface.FrozenDistTargetReadbackProofPosture}",
            $"Frozen dist target readback proof next action: {surface.FrozenDistTargetReadbackProofRecommendedNextAction}",
            $"Frozen dist target readback proof complete: {surface.FrozenDistTargetReadbackProofComplete}",
            $"Tasks: {surface.TaskCount}",
            $"Review tasks: {surface.ReviewTaskCount}",
            $"Completed tasks: {surface.CompletedTaskCount}",
            "Available actions:",
        };

        lines.Add($"Recoverable cleanup required: {surface.RecoverableCleanupRequired}");
        if (surface.RecoverableCleanupRequired)
        {
            lines.Add($"Recoverable residue count: {surface.RecoverableResidueCount}");
            lines.Add($"Highest recoverable residue severity: {surface.HighestRecoverableResidueSeverity}");
            lines.Add($"Recoverable residue blocks auto-run: {surface.RecoverableResidueBlocksAutoRun}");
            lines.Add($"Recoverable cleanup action id: {surface.RecoverableCleanupActionId}");
            lines.Add($"Recoverable cleanup action mode: {surface.RecoverableCleanupActionMode}");
            lines.Add($"Recoverable cleanup summary: {surface.RecoverableCleanupSummary}");
            lines.Add($"Recoverable cleanup next action: {surface.RecoverableCleanupRecommendedNextAction}");
        }

        lines.AddRange(surface.AvailableActions.Count == 0
            ? ["- none"]
            : surface.AvailableActions.Select(action => $"- {action.ActionId} | {action.Kind} | mode={action.ActionMode} | {action.Label} | {action.Command}"));
        lines.Add("Forbidden auto actions:");
        lines.AddRange(surface.ForbiddenAutoActions.Count == 0
            ? ["- none"]
            : surface.ForbiddenAutoActions.Select(action => $"- {action}"));
        lines.AddRange(
        [
            "Pilot stage statuses:",
        ]);

        foreach (var stage in surface.StageStatuses)
        {
            lines.Add($"- stage {stage.Order}: {stage.StageId} | state={stage.State}");
            lines.Add($"  - command: {stage.Command}");
            lines.Add($"  - summary: {stage.Summary}");
        }

        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Target agent bootstrap missing files:");
        lines.AddRange(surface.TargetAgentBootstrapMissingFiles.Count == 0
            ? ["- none"]
            : surface.TargetAgentBootstrapMissingFiles.Select(file => $"- {file}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
