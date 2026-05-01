using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackMismatchDiagnostics()
    {
        return FormatRuntimePackMismatchDiagnostics(CreateRuntimePackMismatchDiagnosticsService().Build());
    }

    public OperatorCommandResult ApiRuntimePackMismatchDiagnostics()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackMismatchDiagnosticsService().Build()));
    }

    private RuntimePackMismatchDiagnosticsService CreateRuntimePackMismatchDiagnosticsService()
    {
        return new RuntimePackMismatchDiagnosticsService(paths, artifactRepository, executionRunService);
    }

    private static OperatorCommandResult FormatRuntimePackMismatchDiagnostics(RuntimePackMismatchDiagnosticsSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack mismatch diagnostics",
            surface.Summary,
            $"Execution coverage: {surface.ExecutionCoverage.Summary}",
        };

        lines.Add(surface.CurrentAdmission is null
            ? "Current admission: none"
            : $"Current admission: {surface.CurrentAdmission.PackId}@{surface.CurrentAdmission.PackVersion} ({surface.CurrentAdmission.Channel})");
        lines.Add(surface.CurrentSelection is null
            ? "Current selection: none"
            : $"Current selection: {surface.CurrentSelection.PackId}@{surface.CurrentSelection.PackVersion} ({surface.CurrentSelection.Channel})");
        lines.Add(surface.CurrentPolicy.PinActive
            ? $"Current pin: {surface.CurrentPolicy.PackId}@{surface.CurrentPolicy.PackVersion} ({surface.CurrentPolicy.Channel})"
            : "Current pin: none");

        if (surface.Diagnostics.Count == 0)
        {
            lines.Add("Diagnostics: none");
            return OperatorCommandResult.Success(lines.ToArray());
        }

        lines.Add("Diagnostics:");
        foreach (var diagnostic in surface.Diagnostics)
        {
            lines.Add($"- {diagnostic.DiagnosticCode} [{diagnostic.Severity}]: {diagnostic.Summary}");
            if (!string.IsNullOrWhiteSpace(diagnostic.Details))
            {
                lines.Add($"  details: {diagnostic.Details}");
            }

            if (diagnostic.RelatedTaskIds.Count > 0)
            {
                lines.Add($"  related tasks: {string.Join(", ", diagnostic.RelatedTaskIds)}");
            }

            if (diagnostic.RecommendedActions.Count > 0)
            {
                lines.Add($"  next actions: {string.Join(" | ", diagnostic.RecommendedActions)}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
