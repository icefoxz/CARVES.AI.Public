using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeProductClosurePilotGuide()
    {
        return OperatorSurfaceFormatter.RuntimeProductClosurePilotGuide(CreateRuntimeProductClosurePilotGuideService().Build());
    }

    public OperatorCommandResult ApiRuntimeProductClosurePilotGuide()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeProductClosurePilotGuideService().Build()));
    }

    private RuntimeProductClosurePilotGuideService CreateRuntimeProductClosurePilotGuideService()
    {
        return new RuntimeProductClosurePilotGuideService(repoRoot);
    }
}
