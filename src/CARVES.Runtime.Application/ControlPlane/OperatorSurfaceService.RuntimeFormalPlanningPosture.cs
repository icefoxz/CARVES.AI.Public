namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeFormalPlanningPosture()
    {
        return OperatorSurfaceFormatter.RuntimeFormalPlanningPosture(platformDashboardService.BuildFormalPlanningPostureSurface());
    }

    public OperatorCommandResult ApiRuntimeFormalPlanningPosture()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(platformDashboardService.BuildFormalPlanningPostureSurface()));
    }
}
