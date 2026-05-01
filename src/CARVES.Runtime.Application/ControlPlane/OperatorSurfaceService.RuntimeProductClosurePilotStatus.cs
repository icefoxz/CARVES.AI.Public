using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeProductClosurePilotStatus()
    {
        return OperatorSurfaceFormatter.RuntimeProductClosurePilotStatus(CreateRuntimeProductClosurePilotStatusService().Build());
    }

    public OperatorCommandResult ApiRuntimeProductClosurePilotStatus()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeProductClosurePilotStatusService().Build()));
    }

    private RuntimeProductClosurePilotStatusService CreateRuntimeProductClosurePilotStatusService()
    {
        return new RuntimeProductClosurePilotStatusService(
            repoRoot,
            taskGraphService,
            () => CreateRuntimeProductClosurePilotGuideService().Build(),
            () => platformDashboardService.BuildFormalPlanningPostureSurface(),
            () => managedWorkspaceLeaseService.BuildSurface());
    }
}
