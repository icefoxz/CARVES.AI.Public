namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetCommitPlan(RuntimeTargetCommitPlanSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target commit plan",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Hygiene document: {surface.HygieneDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Commit plan id: {surface.CommitPlanId}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Hygiene command entry: {surface.HygieneCommandEntry}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Can stage: {surface.CanStage}",
            $"Can commit after staging: {surface.CanCommitAfterStaging}",
            $"Stage paths: {surface.StagePathCount}",
            $"Excluded paths: {surface.ExcludedPathCount}",
            $"Operator review required paths: {surface.OperatorReviewRequiredPathCount}",
            $"Suggested commit message: {surface.SuggestedCommitMessage}",
            $"Git add command preview: {surface.GitAddCommandPreview}",
            $"Git commit command preview: {surface.GitCommitCommandPreview}",
            $"Hygiene summary: {surface.HygieneSummary}",
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
