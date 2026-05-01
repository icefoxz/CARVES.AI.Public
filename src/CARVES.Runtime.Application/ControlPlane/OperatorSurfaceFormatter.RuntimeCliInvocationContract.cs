namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeCliInvocationContract(RuntimeCliInvocationContractSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime CLI invocation contract",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Invocation guide document: {surface.InvocationGuideDocumentPath}",
            $"CLI distribution guide document: {surface.CliDistributionGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Invocation contract complete: {surface.InvocationContractComplete}",
            $"Recommended invocation mode: {surface.RecommendedInvocationMode}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Runtime root has PowerShell wrapper: {surface.RuntimeRootHasPowerShellWrapper}",
            $"Runtime root has cmd wrapper: {surface.RuntimeRootHasCmdWrapper}",
            $"Runtime root has dist manifest: {surface.RuntimeRootHasDistManifest}",
            $"Runtime root has solution: {surface.RuntimeRootHasSolution}",
            $"Invocation lanes: {surface.InvocationLaneCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Invocation lane list:",
        };

        lines.AddRange(surface.InvocationLanes.Count == 0
            ? ["- none"]
            : surface.InvocationLanes.Select(lane => $"- {lane.LaneId} | mode={lane.InvocationMode} | stability={lane.StabilityPosture} | command={lane.CommandPattern} | boundary={lane.Boundary}"));
        lines.Add("Required readback commands:");
        lines.AddRange(surface.RequiredReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredReadbackCommands.Select(command => $"- {command}"));
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
