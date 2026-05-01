using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeCliActivationPlan()
    {
        return OperatorSurfaceFormatter.RuntimeCliActivationPlan(CreateRuntimeCliActivationPlanService().Build());
    }

    public OperatorCommandResult ApiRuntimeCliActivationPlan()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeCliActivationPlanService().Build()));
    }

    private RuntimeCliActivationPlanService CreateRuntimeCliActivationPlanService()
    {
        return new RuntimeCliActivationPlanService(repoRoot);
    }
}
