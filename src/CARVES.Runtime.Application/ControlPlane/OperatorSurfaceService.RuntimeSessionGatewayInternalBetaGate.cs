using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayInternalBetaGate()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayInternalBetaGate(
            CreateRuntimeSessionGatewayInternalBetaGateService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayInternalBetaGate()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayInternalBetaGateService().Build()));
    }

    private RuntimeSessionGatewayInternalBetaGateService CreateRuntimeSessionGatewayInternalBetaGateService()
    {
        return new RuntimeSessionGatewayInternalBetaGateService(
            repoRoot,
            () => CreateRuntimeSessionGatewayPrivateAlphaHandoffService().Build(),
            () => CreateRuntimeSessionGatewayRepeatabilityService().Build());
    }
}
