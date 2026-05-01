using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetDistBindingPlan()
    {
        return OperatorSurfaceFormatter.RuntimeTargetDistBindingPlan(CreateRuntimeTargetDistBindingPlanService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetDistBindingPlan()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetDistBindingPlanService().Build()));
    }

    private RuntimeTargetDistBindingPlanService CreateRuntimeTargetDistBindingPlanService()
    {
        return new RuntimeTargetDistBindingPlanService(repoRoot);
    }
}
