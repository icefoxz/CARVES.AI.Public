namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeGuidedPlanningBoundary(RuntimeGuidedPlanningBoundarySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime guided planning boundary",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Workbench boundary path: {surface.WorkbenchBoundaryPath}",
            $"Session Gateway path: {surface.SessionGatewayPath}",
            $"First-run packet path: {surface.FirstRunPacketPath}",
            $"Quickstart guide path: {surface.QuickstartGuidePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Truth owner: {surface.TruthOwner}",
            $"Interactive shell owner: {surface.InteractiveShellOwner}",
            $"Focus field: {surface.FocusField}",
            $"Focus effect: {surface.FocusEffect}",
            $"Preferred interactive projection: {surface.PreferredInteractiveProjection}",
            $"Auxiliary graph projections: {surface.AuxiliaryGraphProjections.Count}",
        };

        lines.AddRange(surface.AuxiliaryGraphProjections.Select(item => $"- auxiliary-graph-projection: {item}"));
        lines.Add($"Official lifecycle values: {surface.OfficialLifecycle.Count}");
        lines.AddRange(surface.OfficialLifecycle.Select(item => $"- official-lifecycle: {item}"));
        lines.Add($"Planning postures: {surface.PlanningPostures.Count}");
        lines.AddRange(surface.PlanningPostures.Select(item => $"- planning-posture: {item}"));
        lines.Add($"Planning objects: {surface.PlanningObjects.Count}");
        lines.AddRange(surface.PlanningObjects.Select(item =>
            $"- planning-object: {item.ObjectId} | truth-role={item.TruthRole} | writeback={item.WritebackEligibility} | projection={item.ProjectionUse}"));
        lines.Add($"Allowed projection surfaces: {surface.AllowedProjectionSurfaces.Count}");
        lines.AddRange(surface.AllowedProjectionSurfaces.Select(item => $"- allowed-projection-surface: {item}"));
        lines.Add($"Writeback commands: {surface.WritebackCommands.Count}");
        lines.AddRange(surface.WritebackCommands.Select(item => $"- writeback-command: {item}"));
        lines.Add($"Blocked claims: {surface.BlockedClaims.Count}");
        lines.AddRange(surface.BlockedClaims.Select(item => $"- blocked-claim: {item}"));
        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
