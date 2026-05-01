using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeCoreAdapterBoundary()
    {
        return OperatorSurfaceFormatter.RuntimeCoreAdapterBoundary(CreateRuntimeCoreAdapterBoundaryService().Build());
    }

    public OperatorCommandResult ApiRuntimeCoreAdapterBoundary()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeCoreAdapterBoundaryService().Build()));
    }

    private RuntimeCoreAdapterBoundaryService CreateRuntimeCoreAdapterBoundaryService()
    {
        return new RuntimeCoreAdapterBoundaryService(repoRoot, paths, systemConfig);
    }
}
