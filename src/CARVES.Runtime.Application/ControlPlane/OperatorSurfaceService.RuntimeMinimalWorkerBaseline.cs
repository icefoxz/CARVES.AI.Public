using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeMinimalWorkerBaseline()
    {
        return OperatorSurfaceFormatter.RuntimeMinimalWorkerBaseline(CreateRuntimeMinimalWorkerBaselineService().Build());
    }

    public OperatorCommandResult ApiRuntimeMinimalWorkerBaseline()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeMinimalWorkerBaselineService().Build()));
    }

    private RuntimeMinimalWorkerBaselineService CreateRuntimeMinimalWorkerBaselineService()
    {
        return new RuntimeMinimalWorkerBaselineService(repoRoot, paths);
    }
}
