namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetCommitHygiene(RuntimeTargetCommitHygieneSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target commit hygiene",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Can proceed to commit: {surface.CanProceedToCommit}",
            $"Dirty paths: {surface.DirtyPathCount}",
            $"Commit candidate paths: {surface.CommitCandidatePathCount}",
            $"Official truth paths: {surface.OfficialTruthPathCount}",
            $"Target output candidate paths: {surface.TargetOutputCandidatePathCount}",
            $"Local residue paths: {surface.LocalResiduePathCount}",
            $"Unclassified paths: {surface.UnclassifiedPathCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Dirty path classifications:",
        };

        lines.AddRange(surface.DirtyPaths.Count == 0
            ? ["- none"]
            : surface.DirtyPaths.Select(path => $"- {path.StatusCode} {path.Path} | class={path.PathClass} | posture={path.CommitPosture}"));
        lines.Add("Commit candidate paths:");
        lines.AddRange(surface.CommitCandidatePaths.Count == 0
            ? ["- none"]
            : surface.CommitCandidatePaths.Select(path => $"- {path}"));
        lines.Add("Excluded paths:");
        lines.AddRange(surface.ExcludedPaths.Count == 0
            ? ["- none"]
            : surface.ExcludedPaths.Select(path => $"- {path}"));
        lines.Add("Operator review required paths:");
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
