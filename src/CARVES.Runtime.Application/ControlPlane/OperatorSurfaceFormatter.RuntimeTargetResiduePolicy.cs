namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetResiduePolicy(RuntimeTargetResiduePolicySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target residue policy",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Commit closure document: {surface.CommitClosureDocumentPath}",
            $"Commit plan document: {surface.CommitPlanDocumentPath}",
            $"Commit hygiene document: {surface.CommitHygieneDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Commit closure posture: {surface.CommitClosurePosture}",
            $"Commit plan posture: {surface.CommitPlanPosture}",
            $"Commit plan id: {surface.CommitPlanId}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Target git worktree clean: {surface.TargetGitWorktreeClean}",
            $"Commit closure complete: {surface.CommitClosureComplete}",
            $"Residue policy ready: {surface.ResiduePolicyReady}",
            $"Product proof can remain complete: {surface.ProductProofCanRemainComplete}",
            $"Can keep residue local: {surface.CanKeepResidueLocal}",
            $"Can add ignore after review: {surface.CanAddIgnoreAfterReview}",
            $"Stage paths: {surface.StagePathCount}",
            $"Residue paths: {surface.ResiduePathCount}",
            $"Operator review required paths: {surface.OperatorReviewRequiredPathCount}",
            $"Suggested ignore entries: {surface.SuggestedIgnoreEntryCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Stage path list:",
        };

        lines.AddRange(surface.StagePaths.Count == 0
            ? ["- none"]
            : surface.StagePaths.Select(path => $"- {path}"));
        lines.Add("Residue path list:");
        lines.AddRange(surface.ResiduePaths.Count == 0
            ? ["- none"]
            : surface.ResiduePaths.Select(path => $"- {path}"));
        lines.Add("Operator review required path list:");
        lines.AddRange(surface.OperatorReviewRequiredPaths.Count == 0
            ? ["- none"]
            : surface.OperatorReviewRequiredPaths.Select(path => $"- {path}"));
        lines.Add("Suggested .gitignore entries:");
        lines.AddRange(surface.SuggestedIgnoreEntries.Count == 0
            ? ["- none"]
            : surface.SuggestedIgnoreEntries.Select(entry => $"- {entry}"));
        lines.Add("Ignore suggestions:");
        if (surface.IgnoreSuggestions.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var suggestion in surface.IgnoreSuggestions)
            {
                lines.Add($"- {suggestion.Entry} | paths={suggestion.MatchingPathCount} | reason={suggestion.Reason}");
                foreach (var path in suggestion.MatchingPaths)
                {
                    lines.Add($"  path: {path}");
                }
            }
        }

        lines.Add("Boundary rules:");
        lines.AddRange(surface.BoundaryRules.Count == 0
            ? ["- none"]
            : surface.BoundaryRules.Select(rule => $"- {rule}"));
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
