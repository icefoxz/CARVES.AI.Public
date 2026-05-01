namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentValidationBundle(RuntimeAgentValidationBundleSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent validation bundle",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Guide path: {surface.GuidePath}",
            $"Workmap path: {surface.WorkmapPath}",
            $"Architecture path: {surface.ArchitecturePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Validation ownership: {surface.ValidationOwnership}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Validation lanes: {surface.Lanes.Count}",
        };

        foreach (var lane in surface.Lanes)
        {
            lines.Add($"- lane: {lane.LaneId}");
            lines.Add($"  summary: {lane.Summary}");
            lines.Add($"  runtime surfaces: {(lane.RuntimeSurfaceRefs.Count == 0 ? "(none)" : string.Join(", ", lane.RuntimeSurfaceRefs))}");
            lines.Add($"  stable evidence: {(lane.StableEvidencePaths.Count == 0 ? "(none)" : string.Join(", ", lane.StableEvidencePaths))}");
            lines.Add($"  test files: {(lane.TestFileRefs.Count == 0 ? "(none)" : string.Join(", ", lane.TestFileRefs))}");
            lines.Add($"  validation commands: {lane.ValidationCommands.Count}");
            lines.AddRange(lane.ValidationCommands.Select(item => $"  - command: {item}"));
            lines.AddRange(lane.Notes.Select(item => $"  note: {item}"));
        }

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
