using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackDistributionBoundary()
    {
        return FormatRuntimePackDistributionBoundary(CreateRuntimePackDistributionBoundaryService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackDistributionBoundary()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackDistributionBoundaryService().BuildSurface()));
    }

    private RuntimePackDistributionBoundaryService CreateRuntimePackDistributionBoundaryService()
    {
        return new RuntimePackDistributionBoundaryService(artifactRepository);
    }

    private static OperatorCommandResult FormatRuntimePackDistributionBoundary(RuntimePackDistributionBoundarySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack distribution boundary",
            surface.Summary,
            $"Current truth: {surface.CurrentTruth.Summary}",
            "Local capabilities:",
        };

        foreach (var capability in surface.LocalCapabilities)
        {
            lines.Add($"- {capability.CapabilityId}: {capability.Summary}");
        }

        lines.Add("Closed future capabilities:");
        foreach (var capability in surface.ClosedFutureCapabilities)
        {
            lines.Add($"- {capability.CapabilityId}: {capability.Summary}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
