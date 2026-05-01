using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeFirstRunOperatorPacket()
    {
        return OperatorSurfaceFormatter.RuntimeFirstRunOperatorPacket(
            CreateRuntimeFirstRunOperatorPacketService().Build());
    }

    public OperatorCommandResult ApiRuntimeFirstRunOperatorPacket()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeFirstRunOperatorPacketService().Build()));
    }

    private RuntimeFirstRunOperatorPacketService CreateRuntimeFirstRunOperatorPacketService()
    {
        return new RuntimeFirstRunOperatorPacketService(
            repoRoot,
            () => CreateRuntimeSessionGatewayInternalBetaGateService().Build());
    }
}
