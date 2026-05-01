namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentWorkingModes()
    {
        return OperatorSurfaceFormatter.RuntimeAgentWorkingModes(platformDashboardService.BuildAgentWorkingModesSurface());
    }

    public OperatorCommandResult ApiRuntimeAgentWorkingModes()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(platformDashboardService.BuildAgentWorkingModesSurface()));
    }
}
