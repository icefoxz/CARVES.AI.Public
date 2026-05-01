using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeGuidedPlanningBoundary()
    {
        return OperatorSurfaceFormatter.RuntimeGuidedPlanningBoundary(CreateRuntimeGuidedPlanningBoundaryService().Build());
    }

    public OperatorCommandResult ApiRuntimeGuidedPlanningBoundary()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeGuidedPlanningBoundaryService().Build()));
    }

    private RuntimeGuidedPlanningBoundaryService CreateRuntimeGuidedPlanningBoundaryService()
    {
        return new RuntimeGuidedPlanningBoundaryService(repoRoot);
    }
}
