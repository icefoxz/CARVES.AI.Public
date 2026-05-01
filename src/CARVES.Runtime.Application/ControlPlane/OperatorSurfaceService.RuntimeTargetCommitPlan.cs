using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetCommitPlan()
    {
        return OperatorSurfaceFormatter.RuntimeTargetCommitPlan(CreateRuntimeTargetCommitPlanService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetCommitPlan()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetCommitPlanService().Build()));
    }

    private RuntimeTargetCommitPlanService CreateRuntimeTargetCommitPlanService()
    {
        return new RuntimeTargetCommitPlanService(repoRoot);
    }
}
