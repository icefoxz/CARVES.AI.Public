using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeRemoteWorkerQualification()
    {
        return FormatRuntimeRemoteWorkerQualification(new RuntimeRemoteWorkerQualificationService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimeRemoteWorkerQualification()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(new RuntimeRemoteWorkerQualificationService().BuildSurface()));
    }

    private static OperatorCommandResult FormatRuntimeRemoteWorkerQualification(RuntimeRemoteWorkerQualificationSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime remote worker qualification",
            surface.Summary,
        };

        foreach (var policy in surface.CurrentPolicies)
        {
            lines.Add($"Provider/backend: {policy.ProviderId}/{policy.BackendId}");
            lines.Add($"Policy: {policy.PolicyId}");
            lines.Add($"Routing profile: {policy.RoutingProfileId}");
            lines.Add($"Protocol family: {policy.ProtocolFamily}");
            lines.Add($"Recorded at: {policy.RecordedAt:O}");
            lines.Add("Qualified lanes:");
            lines.AddRange(policy.Lanes.Where(item => item.Allowed).Select(item =>
                $"- {item.RoutingIntent}: {item.Summary} [{(item.Constraints.Length == 0 ? "no extra constraints" : string.Join("; ", item.Constraints))}]"));
            lines.Add("Closed lanes:");
            lines.AddRange(policy.Lanes.Where(item => !item.Allowed).Select(item =>
                $"- {item.RoutingIntent}: {item.Summary} [{(item.Constraints.Length == 0 ? "no extra constraints" : string.Join("; ", item.Constraints))}]"));
            if (policy.ChecksPassed.Length > 0)
            {
                lines.Add("Checks passed:");
                lines.AddRange(policy.ChecksPassed.Select(item => $"- {item}"));
            }
        }

        if (surface.Notes.Length > 0)
        {
            lines.Add("Notes:");
            lines.AddRange(surface.Notes.Select(item => $"- {item}"));
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
