using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackAdmissionPolicy()
    {
        return FormatRuntimePackAdmissionPolicy(CreateRuntimePackAdmissionPolicyService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackAdmissionPolicy()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackAdmissionPolicyService().BuildSurface()));
    }

    private RuntimePackAdmissionPolicyService CreateRuntimePackAdmissionPolicyService()
    {
        return new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
    }

    private static OperatorCommandResult FormatRuntimePackAdmissionPolicy(RuntimePackAdmissionPolicySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack admission policy",
            $"Runtime standard: {surface.RuntimeStandardVersion}",
            surface.Summary,
            $"Allowed channels: {(surface.CurrentPolicy.AllowedChannels.Count == 0 ? "(none)" : string.Join(", ", surface.CurrentPolicy.AllowedChannels))}",
            $"Allowed pack types: {(surface.CurrentPolicy.AllowedPackTypes.Count == 0 ? "(none)" : string.Join(", ", surface.CurrentPolicy.AllowedPackTypes))}",
            $"Require signature: {surface.CurrentPolicy.RequireSignature}",
            $"Require provenance: {surface.CurrentPolicy.RequireProvenance}",
        };

        if (surface.CurrentPolicy.ChecksPassed.Length > 0)
        {
            lines.Add($"Checks passed: {string.Join("; ", surface.CurrentPolicy.ChecksPassed)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
