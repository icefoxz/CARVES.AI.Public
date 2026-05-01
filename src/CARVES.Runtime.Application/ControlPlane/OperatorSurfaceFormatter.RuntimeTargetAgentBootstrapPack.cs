namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetAgentBootstrapPack(RuntimeTargetAgentBootstrapPackSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target agent bootstrap pack",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"Materialize command: {surface.MaterializeCommandEntry}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Target bootstrap path: {surface.TargetAgentBootstrapPath}",
            $"Target bootstrap exists: {surface.TargetAgentBootstrapExists}",
            $"Root AGENTS path: {surface.RootAgentsPath}",
            $"Root AGENTS exists: {surface.RootAgentsExists}",
            $"Root AGENTS contains CARVES entry: {surface.RootAgentsContainsCarvesEntry}",
            $"Root AGENTS integration posture: {surface.RootAgentsIntegrationPosture}",
            $"Project-local launcher path: {surface.ProjectLocalLauncherPath}",
            $"Project-local launcher exists: {surface.ProjectLocalLauncherExists}",
            $"Agent start markdown path: {surface.AgentStartMarkdownPath}",
            $"Agent start markdown exists: {surface.AgentStartMarkdownExists}",
            $"Agent start JSON path: {surface.AgentStartJsonPath}",
            $"Agent start JSON exists: {surface.AgentStartJsonExists}",
            $"Visible agent start path: {surface.VisibleAgentStartPath}",
            $"Visible agent start exists: {surface.VisibleAgentStartExists}",
            $"Can materialize: {surface.CanMaterialize}",
            $"Write requested: {surface.WriteRequested}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Missing files:",
        };

        lines.AddRange(surface.MissingFiles.Count == 0
            ? ["- none"]
            : surface.MissingFiles.Select(file => $"- {file}"));
        lines.Add("Materialized files:");
        lines.AddRange(surface.MaterializedFiles.Count == 0
            ? ["- none"]
            : surface.MaterializedFiles.Select(file => $"- {file}"));
        lines.Add("Skipped existing files:");
        lines.AddRange(surface.SkippedFiles.Count == 0
            ? ["- none"]
            : surface.SkippedFiles.Select(file => $"- {file}"));
        lines.Add("Root AGENTS suggested patch:");
        lines.Add(string.IsNullOrWhiteSpace(surface.RootAgentsSuggestedPatch)
            ? "- none"
            : surface.RootAgentsSuggestedPatch);
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
