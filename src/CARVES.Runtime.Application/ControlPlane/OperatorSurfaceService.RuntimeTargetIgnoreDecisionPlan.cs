using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetIgnoreDecisionPlan()
    {
        return OperatorSurfaceFormatter.RuntimeTargetIgnoreDecisionPlan(CreateRuntimeTargetIgnoreDecisionPlanService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetIgnoreDecisionPlan()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetIgnoreDecisionPlanService().Build()));
    }

    private RuntimeTargetIgnoreDecisionPlanService CreateRuntimeTargetIgnoreDecisionPlanService()
    {
        return new RuntimeTargetIgnoreDecisionPlanService(repoRoot);
    }
}
