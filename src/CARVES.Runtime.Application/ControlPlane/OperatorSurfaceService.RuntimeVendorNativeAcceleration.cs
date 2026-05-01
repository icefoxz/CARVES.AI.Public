namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeVendorNativeAcceleration()
    {
        return OperatorSurfaceFormatter.RuntimeVendorNativeAcceleration(platformDashboardService.BuildVendorNativeAccelerationSurface());
    }

    public OperatorCommandResult ApiRuntimeVendorNativeAcceleration()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(platformDashboardService.BuildVendorNativeAccelerationSurface()));
    }
}
