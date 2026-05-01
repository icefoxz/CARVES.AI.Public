namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeProductPilotProof(RuntimeProductPilotProofSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime product pilot proof",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Pilot guide document: {surface.PilotGuideDocumentPath}",
            $"Pilot status document: {surface.PilotStatusDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Product pilot proof complete: {surface.ProductPilotProofComplete}",
            $"Local dist freshness smoke posture: {surface.LocalDistFreshnessSmokePosture}",
            $"Local dist freshness smoke ready: {surface.LocalDistFreshnessSmokeReady}",
            $"Local dist freshness smoke source commit: {surface.LocalDistFreshnessSmokeSourceCommit}",
            $"Local dist handoff posture: {surface.LocalDistHandoffPosture}",
            $"Stable external consumption ready: {surface.StableExternalConsumptionReady}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Runtime dist manifest version: {surface.RuntimeDistManifestVersion}",
            $"Runtime dist manifest source commit: {surface.RuntimeDistManifestSourceCommit}",
            $"Frozen dist target readback proof posture: {surface.FrozenDistTargetReadbackProofPosture}",
            $"Frozen dist target readback proof complete: {surface.FrozenDistTargetReadbackProofComplete}",
            $"Target commit closure posture: {surface.TargetCommitClosurePosture}",
            $"Target commit plan posture: {surface.TargetCommitPlanPosture}",
            $"Target residue policy posture: {surface.TargetResiduePolicyPosture}",
            $"Target ignore decision plan posture: {surface.TargetIgnoreDecisionPlanPosture}",
            $"Target ignore decision record posture: {surface.TargetIgnoreDecisionRecordPosture}",
            $"Commit plan id: {surface.CommitPlanId}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Target git worktree clean: {surface.TargetGitWorktreeClean}",
            $"Target commit closure complete: {surface.TargetCommitClosureComplete}",
            $"Target residue policy ready: {surface.TargetResiduePolicyReady}",
            $"Product proof can remain complete with residue: {surface.ProductProofCanRemainCompleteWithResidue}",
            $"Target ignore decision plan ready: {surface.TargetIgnoreDecisionPlanReady}",
            $"Target ignore decision record ready: {surface.TargetIgnoreDecisionRecordReady}",
            $"Target ignore decision record audit ready: {surface.TargetIgnoreDecisionRecordAuditReady}",
            $"Target ignore decision record commit ready: {surface.TargetIgnoreDecisionRecordCommitReady}",
            $"Ignore decision required: {surface.IgnoreDecisionRequired}",
            $"Can apply ignore after review: {surface.CanApplyIgnoreAfterReview}",
            $"Can stage: {surface.CanStage}",
            $"Stage paths: {surface.StagePathCount}",
            $"Excluded paths: {surface.ExcludedPathCount}",
            $"Operator review required paths: {surface.OperatorReviewRequiredPathCount}",
            $"Suggested ignore entries: {surface.SuggestedIgnoreEntryCount}",
            $"Missing ignore entries: {surface.MissingIgnoreEntryCount}",
            $"Required ignore decision entries: {surface.RequiredIgnoreDecisionEntryCount}",
            $"Recorded ignore decision entries: {surface.RecordedIgnoreDecisionEntryCount}",
            $"Missing ignore decision entries: {surface.MissingIgnoreDecisionEntryCount}",
            $"Invalid ignore decision records: {surface.InvalidIgnoreDecisionRecordCount}",
            $"Malformed ignore decision records: {surface.MalformedIgnoreDecisionRecordCount}",
            $"Conflicting ignore decision entries: {surface.ConflictingIgnoreDecisionEntryCount}",
            $"Uncommitted ignore decision records: {surface.UncommittedIgnoreDecisionRecordCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required readback commands:",
        };

        lines.AddRange(surface.RequiredReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredReadbackCommands.Select(command => $"- {command}"));
        lines.Add("Stage path list:");
        lines.AddRange(surface.StagePaths.Count == 0
            ? ["- none"]
            : surface.StagePaths.Select(path => $"- {path}"));
        lines.Add("Excluded path list:");
        lines.AddRange(surface.ExcludedPaths.Count == 0
            ? ["- none"]
            : surface.ExcludedPaths.Select(path => $"- {path}"));
        lines.Add("Operator review required path list:");
        lines.AddRange(surface.OperatorReviewRequiredPaths.Count == 0
            ? ["- none"]
            : surface.OperatorReviewRequiredPaths.Select(path => $"- {path}"));
        lines.Add("Suggested .gitignore entries:");
        lines.AddRange(surface.SuggestedIgnoreEntries.Count == 0
            ? ["- none"]
            : surface.SuggestedIgnoreEntries.Select(entry => $"- {entry}"));
        lines.Add("Missing .gitignore entries:");
        lines.AddRange(surface.MissingIgnoreEntries.Count == 0
            ? ["- none"]
            : surface.MissingIgnoreEntries.Select(entry => $"- {entry}"));
        lines.Add("Missing ignore decision entries:");
        lines.AddRange(surface.MissingIgnoreDecisionEntries.Count == 0
            ? ["- none"]
            : surface.MissingIgnoreDecisionEntries.Select(entry => $"- {entry}"));
        lines.Add("Ignore decision records:");
        lines.AddRange(surface.IgnoreDecisionRecordIds.Count == 0
            ? ["- none"]
            : surface.IgnoreDecisionRecordIds.Select(recordId => $"- {recordId}"));
        lines.Add("Invalid ignore decision record paths:");
        lines.AddRange(surface.InvalidIgnoreDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.InvalidIgnoreDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Malformed ignore decision record paths:");
        lines.AddRange(surface.MalformedIgnoreDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.MalformedIgnoreDecisionRecordPaths.Select(path => $"- {path}"));
        lines.Add("Conflicting ignore decision entries:");
        lines.AddRange(surface.ConflictingIgnoreDecisionEntries.Count == 0
            ? ["- none"]
            : surface.ConflictingIgnoreDecisionEntries.Select(entry => $"- {entry}"));
        lines.Add("Uncommitted ignore decision record paths:");
        lines.AddRange(surface.UncommittedIgnoreDecisionRecordPaths.Count == 0
            ? ["- none"]
            : surface.UncommittedIgnoreDecisionRecordPaths.Select(path => $"- {path}"));
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
