using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeBetaProgramStatus()
    {
        return OperatorSurfaceFormatter.RuntimeBetaProgramStatus(
            CreateRuntimeBetaProgramStatusService().Build());
    }

    public OperatorCommandResult ApiRuntimeBetaProgramStatus()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeBetaProgramStatusService().Build()));
    }

    private RuntimeBetaProgramStatusService CreateRuntimeBetaProgramStatusService()
    {
        return new RuntimeBetaProgramStatusService(
            repoRoot,
            () => CreateRuntimeSessionGatewayInternalBetaGateService().Build(),
            () => CreateRuntimeFirstRunOperatorPacketService().Build(),
            () => CreateRuntimeSessionGatewayInternalBetaExitContractService().Build());
    }
}
