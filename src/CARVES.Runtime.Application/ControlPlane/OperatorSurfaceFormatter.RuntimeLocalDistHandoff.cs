namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeLocalDistHandoff(RuntimeLocalDistHandoffSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime local dist handoff",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Local dist guide document: {surface.LocalDistGuideDocumentPath}",
            $"CLI distribution guide document: {surface.CliDistributionGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Stable external consumption ready: {surface.StableExternalConsumptionReady}",
            $"External target bound to runtime root: {surface.ExternalTargetBoundToRuntimeRoot}",
            $"Runtime root matches repo root: {surface.RuntimeRootMatchesRepoRoot}",
            $"Runtime root has manifest: {surface.RuntimeRootHasManifest}",
            $"Runtime root has version: {surface.RuntimeRootHasVersion}",
            $"Runtime root has wrapper: {surface.RuntimeRootHasWrapper}",
            $"Runtime root has .git: {surface.RuntimeRootHasGitDirectory}",
            $"Runtime root has solution: {surface.RuntimeRootHasSolution}",
            $"Manifest schema version: {surface.ManifestSchemaVersion}",
            $"Manifest version: {surface.ManifestVersion}",
            $"Manifest source commit: {surface.ManifestSourceCommit}",
            $"Manifest output path: {surface.ManifestOutputPath}",
            $"Version file value: {surface.VersionFileValue}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required smoke commands:",
        };

        lines.AddRange(surface.RequiredSmokeCommands.Count == 0
            ? ["- none"]
            : surface.RequiredSmokeCommands.Select(command => $"- {command}"));
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
