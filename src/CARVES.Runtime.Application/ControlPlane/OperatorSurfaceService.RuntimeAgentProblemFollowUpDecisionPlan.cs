using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemFollowUpDecisionPlan()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemFollowUpDecisionPlan(CreateRuntimeAgentProblemFollowUpDecisionPlanService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemFollowUpDecisionPlan()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemFollowUpDecisionPlanService().Build()));
    }

    private RuntimeAgentProblemFollowUpDecisionPlanService CreateRuntimeAgentProblemFollowUpDecisionPlanService()
    {
        return new RuntimeAgentProblemFollowUpDecisionPlanService(
            repoRoot,
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
