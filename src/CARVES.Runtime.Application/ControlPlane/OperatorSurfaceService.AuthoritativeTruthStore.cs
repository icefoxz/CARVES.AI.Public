using System.Text.Json;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectAuthoritativeTruthStore()
    {
        var surface = new AuthoritativeTruthStoreSurfaceService(paths, controlPlaneLockService).Build();
        return new OperatorCommandResult(0, OperatorSurfaceFormatter.FormatAuthoritativeTruthStore(surface));
    }

    public OperatorCommandResult ApiAuthoritativeTruthStore()
    {
        var surface = new AuthoritativeTruthStoreSurfaceService(paths, controlPlaneLockService).Build();
        return OperatorCommandResult.Success(JsonSerializer.Serialize(surface, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));
    }
}
