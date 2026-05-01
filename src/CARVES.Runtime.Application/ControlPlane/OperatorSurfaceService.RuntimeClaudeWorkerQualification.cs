using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeClaudeWorkerQualification()
    {
        return FormatRuntimeClaudeWorkerQualification(new RuntimeClaudeWorkerQualificationService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimeClaudeWorkerQualification()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(new RuntimeClaudeWorkerQualificationService().BuildSurface()));
    }

    private static OperatorCommandResult FormatRuntimeClaudeWorkerQualification(RuntimeClaudeWorkerQualificationSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Claude worker qualification",
            surface.Summary,
            $"Policy: {surface.CurrentPolicy.PolicyId}",
            $"Provider/backend: {surface.CurrentPolicy.ProviderId}/{surface.CurrentPolicy.BackendId}",
            $"Recorded at: {surface.CurrentPolicy.RecordedAt:O}",
        };

        var allowed = surface.CurrentPolicy.Lanes.Where(item => item.Allowed).ToArray();
        var closed = surface.CurrentPolicy.Lanes.Where(item => !item.Allowed).ToArray();

        lines.Add("Qualified lanes:");
        lines.AddRange(allowed.Select(item =>
            $"- {item.RoutingIntent}: {item.Summary} [{(item.Constraints.Length == 0 ? "no extra constraints" : string.Join("; ", item.Constraints))}]"));

        lines.Add("Closed lanes:");
        lines.AddRange(closed.Select(item =>
            $"- {item.RoutingIntent}: {item.Summary} [{(item.Constraints.Length == 0 ? "no extra constraints" : string.Join("; ", item.Constraints))}]"));

        if (surface.CurrentPolicy.ChecksPassed.Length > 0)
        {
            lines.Add("Checks passed:");
            lines.AddRange(surface.CurrentPolicy.ChecksPassed.Select(item => $"- {item}"));
        }

        if (surface.Notes.Length > 0)
        {
            lines.Add("Notes:");
            lines.AddRange(surface.Notes.Select(item => $"- {item}"));
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
