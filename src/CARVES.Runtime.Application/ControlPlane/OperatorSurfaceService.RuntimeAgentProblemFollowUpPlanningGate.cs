using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemFollowUpPlanningGate()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemFollowUpPlanningGate(CreateRuntimeAgentProblemFollowUpPlanningGateService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemFollowUpPlanningGate()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemFollowUpPlanningGateService().Build()));
    }

    private RuntimeAgentProblemFollowUpPlanningGateService CreateRuntimeAgentProblemFollowUpPlanningGateService()
    {
        return new RuntimeAgentProblemFollowUpPlanningGateService(
            repoRoot,
            () => CreateRuntimeAgentProblemFollowUpPlanningIntakeService().Build(),
            () => platformDashboardService.BuildFormalPlanningPostureSurface());
    }
}
