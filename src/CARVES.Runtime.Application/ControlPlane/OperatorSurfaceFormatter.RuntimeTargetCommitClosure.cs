namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetCommitClosure(RuntimeTargetCommitClosureSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target commit closure",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Commit plan document: {surface.CommitPlanDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Commit plan command entry: {surface.CommitPlanCommandEntry}",
            $"Commit plan posture: {surface.CommitPlanPosture}",
            $"Commit plan id: {surface.CommitPlanId}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Target git worktree clean: {surface.TargetGitWorktreeClean}",
            $"Commit closure complete: {surface.CommitClosureComplete}",
            $"Can stage: {surface.CanStage}",
            $"Stage paths: {surface.StagePathCount}",
            $"Excluded paths: {surface.ExcludedPathCount}",
            $"Operator review required paths: {surface.OperatorReviewRequiredPathCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Stage path list:",
        };

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
