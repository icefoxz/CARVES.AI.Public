namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetIgnoreDecisionPlan(RuntimeTargetIgnoreDecisionPlanSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target ignore decision plan",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Residue policy document: {surface.ResiduePolicyDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Ignore decision plan id: {surface.IgnoreDecisionPlanId}",
            $"Residue policy posture: {surface.ResiduePolicyPosture}",
            $"Commit closure complete: {surface.CommitClosureComplete}",
            $"Residue policy ready: {surface.ResiduePolicyReady}",
            $"Product proof can remain complete: {surface.ProductProofCanRemainComplete}",
            $"Ignore decision plan ready: {surface.IgnoreDecisionPlanReady}",
            $"Ignore decision required: {surface.IgnoreDecisionRequired}",
            $"Can keep residue local: {surface.CanKeepResidueLocal}",
            $"Can apply ignore after review: {surface.CanApplyIgnoreAfterReview}",
            $"Gitignore exists: {surface.GitIgnoreExists}",
            $"Residue paths: {surface.ResiduePathCount}",
            $"Suggested ignore entries: {surface.SuggestedIgnoreEntryCount}",
            $"Missing ignore entries: {surface.MissingIgnoreEntryCount}",
            $"Decision candidates: {surface.DecisionCandidateCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Suggested .gitignore entries:",
        };

        lines.AddRange(surface.SuggestedIgnoreEntries.Count == 0
            ? ["- none"]
            : surface.SuggestedIgnoreEntries.Select(entry => $"- {entry}"));
        lines.Add("Missing .gitignore entries:");
        lines.AddRange(surface.MissingIgnoreEntries.Count == 0
            ? ["- none"]
            : surface.MissingIgnoreEntries.Select(entry => $"- {entry}"));
        lines.Add(".gitignore patch preview:");
        lines.AddRange(surface.GitIgnorePatchPreview.Count == 0
            ? ["- none"]
            : surface.GitIgnorePatchPreview.Select(line => $"- {line}"));
        lines.Add("Residue path list:");
        lines.AddRange(surface.ResiduePaths.Count == 0
            ? ["- none"]
            : surface.ResiduePaths.Select(path => $"- {path}"));
        lines.Add("Decision candidates:");
        if (surface.DecisionCandidates.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var candidate in surface.DecisionCandidates)
            {
                lines.Add($"- {candidate.Entry} | approval_required={candidate.OperatorApprovalRequired} | already_present={candidate.AlreadyPresentInGitIgnore} | paths={candidate.MatchingPathCount}");
                lines.Add($"  recommended_decision: {candidate.RecommendedDecision}");
                lines.Add($"  reason: {candidate.Reason}");
                lines.Add($"  options: {string.Join(", ", candidate.DecisionOptions)}");
                foreach (var path in candidate.MatchingPaths)
                {
                    lines.Add($"  path: {path}");
                }
            }
        }

        lines.Add("Operator decision checklist:");
        lines.AddRange(surface.OperatorDecisionChecklist.Count == 0
            ? ["- none"]
            : surface.OperatorDecisionChecklist.Select(item => $"- {item}"));
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
