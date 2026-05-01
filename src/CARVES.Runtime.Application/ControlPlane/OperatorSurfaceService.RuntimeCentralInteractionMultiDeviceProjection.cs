using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeCentralInteractionMultiDeviceProjection()
    {
        return OperatorSurfaceFormatter.RuntimeCentralInteractionMultiDeviceProjection(
            CreateRuntimeCentralInteractionMultiDeviceProjectionService().Build());
    }

    public OperatorCommandResult ApiRuntimeCentralInteractionMultiDeviceProjection()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeCentralInteractionMultiDeviceProjectionService().Build()));
    }

    private RuntimeCentralInteractionMultiDeviceProjectionService CreateRuntimeCentralInteractionMultiDeviceProjectionService()
    {
        return new RuntimeCentralInteractionMultiDeviceProjectionService(repoRoot);
    }
}
