using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayInternalBetaExitContract()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayInternalBetaExitContract(
            CreateRuntimeSessionGatewayInternalBetaExitContractService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayInternalBetaExitContract()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayInternalBetaExitContractService().Build()));
    }

    private RuntimeSessionGatewayInternalBetaExitContractService CreateRuntimeSessionGatewayInternalBetaExitContractService()
    {
        return new RuntimeSessionGatewayInternalBetaExitContractService(
            repoRoot,
            () => CreateRuntimeSessionGatewayInternalBetaGateService().Build(),
            () => CreateRuntimeFirstRunOperatorPacketService().Build());
    }
}
