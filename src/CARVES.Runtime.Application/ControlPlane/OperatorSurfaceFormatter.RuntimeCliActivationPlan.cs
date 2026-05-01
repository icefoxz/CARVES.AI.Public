namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeCliActivationPlan(RuntimeCliActivationPlanSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime CLI activation plan",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Activation guide document: {surface.ActivationGuideDocumentPath}",
            $"Invocation guide document: {surface.InvocationGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Activation plan complete: {surface.ActivationPlanComplete}",
            $"Recommended activation lane: {surface.RecommendedActivationLane}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Runtime root has PowerShell wrapper: {surface.RuntimeRootHasPowerShellWrapper}",
            $"Runtime root has cmd wrapper: {surface.RuntimeRootHasCmdWrapper}",
            $"Runtime root has dist manifest: {surface.RuntimeRootHasDistManifest}",
            $"Runtime root on process PATH: {surface.RuntimeRootOnProcessPath}",
            $"CARVES_RUNTIME_ROOT matches: {surface.CarvesRuntimeRootEnvironmentMatches}",
            $"Activation lanes: {surface.ActivationLaneCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Activation lane list:",
        };

        lines.AddRange(surface.ActivationLanes.Count == 0
            ? ["- none"]
            : surface.ActivationLanes.Select(lane => $"- {lane.LaneId} | mode={lane.ActivationMode} | persistence={lane.Persistence} | command={lane.CommandPreview} | boundary={lane.Boundary}"));
        lines.Add("Required smoke commands:");
        lines.AddRange(surface.RequiredSmokeCommands.Count == 0
            ? ["- none"]
            : surface.RequiredSmokeCommands.Select(command => $"- {command}"));
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
