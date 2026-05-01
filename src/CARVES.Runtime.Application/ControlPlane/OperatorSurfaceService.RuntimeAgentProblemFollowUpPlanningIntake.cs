using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemFollowUpPlanningIntake()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemFollowUpPlanningIntake(CreateRuntimeAgentProblemFollowUpPlanningIntakeService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemFollowUpPlanningIntake()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemFollowUpPlanningIntakeService().Build()));
    }

    private RuntimeAgentProblemFollowUpPlanningIntakeService CreateRuntimeAgentProblemFollowUpPlanningIntakeService()
    {
        return new RuntimeAgentProblemFollowUpPlanningIntakeService(
            repoRoot,
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
