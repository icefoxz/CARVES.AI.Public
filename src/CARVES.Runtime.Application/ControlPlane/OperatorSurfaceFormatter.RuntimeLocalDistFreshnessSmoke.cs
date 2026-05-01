namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeLocalDistFreshnessSmoke(RuntimeLocalDistFreshnessSmokeSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime local dist freshness smoke",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Current product closure document: {surface.CurrentProductClosureDocumentPath}",
            $"Current product closure guide: {surface.CurrentProductClosureGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Dist version: {surface.DistVersion}",
            $"Source repo root: {surface.SourceRepoRoot}",
            $"Source repo root exists: {surface.SourceRepoRootExists}",
            $"Source git HEAD detected: {surface.SourceGitHeadDetected}",
            $"Source git HEAD: {EmptyAsNone(surface.SourceGitHead)}",
            $"Source git worktree clean: {surface.SourceGitWorktreeClean}",
            $"Candidate dist root: {surface.CandidateDistRoot}",
            $"Candidate dist exists: {surface.CandidateDistExists}",
            $"Candidate dist has manifest: {surface.CandidateDistHasManifest}",
            $"Candidate dist has version: {surface.CandidateDistHasVersion}",
            $"Candidate dist has wrapper: {surface.CandidateDistHasWrapper}",
            $"Candidate dist published CLI entry: {surface.CandidateDistPublishedCliEntry}",
            $"Candidate dist has published CLI: {surface.CandidateDistHasPublishedCli}",
            $"Candidate dist has phase document: {surface.CandidateDistHasPhaseDocument}",
            $"Candidate dist has guide document: {surface.CandidateDistHasGuideDocument}",
            $"Candidate dist has target binding guide: {surface.CandidateDistHasTargetBindingGuide}",
            $"Candidate dist has local dist guide: {surface.CandidateDistHasLocalDistGuide}",
            $"Candidate dist has CLI distribution guide: {surface.CandidateDistHasCliDistributionGuide}",
            $"Candidate dist has current product closure document: {surface.CandidateDistHasCurrentProductClosureDocument}",
            $"Candidate dist has current product closure guide: {surface.CandidateDistHasCurrentProductClosureGuide}",
            $"Candidate dist has .git: {surface.CandidateDistHasGitDirectory}",
            $"Candidate dist has solution: {surface.CandidateDistHasSolution}",
            $"Manifest schema version: {surface.ManifestSchemaVersion}",
            $"Manifest version: {surface.ManifestVersion}",
            $"Manifest source commit: {surface.ManifestSourceCommit}",
            $"Manifest source repo root: {surface.ManifestSourceRepoRoot}",
            $"Manifest output path: {surface.ManifestOutputPath}",
            $"Manifest published CLI entry: {surface.ManifestPublishedCliEntry}",
            $"Version file value: {surface.VersionFileValue}",
            $"Manifest version matches version file: {surface.ManifestVersionMatchesVersionFile}",
            $"Manifest output matches candidate dist: {surface.ManifestOutputMatchesCandidateDist}",
            $"Manifest source commit matches source HEAD: {surface.ManifestSourceCommitMatchesSourceHead}",
            $"Manifest published CLI entry matches published CLI: {surface.ManifestPublishedCliEntryMatchesPublishedCli}",
            $"Local dist freshness smoke ready: {surface.LocalDistFreshnessSmokeReady}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required source commands:",
        };

        lines.AddRange(surface.RequiredSourceCommands.Count == 0
            ? ["- none"]
            : surface.RequiredSourceCommands.Select(command => $"- {command}"));
        lines.Add("Required dist readback commands:");
        lines.AddRange(surface.RequiredDistReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredDistReadbackCommands.Select(command => $"- {command}"));
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
