using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectPacketEnforcement(string taskId)
    {
        return OperatorSurfaceFormatter.PacketEnforcement(CreatePacketEnforcementSurfaceService().Build(taskId));
    }

    public OperatorCommandResult ApiPacketEnforcement(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreatePacketEnforcementSurfaceService().Build(taskId)));
    }

    private PacketEnforcementSurfaceService CreatePacketEnforcementSurfaceService()
    {
        return new PacketEnforcementSurfaceService(new PacketEnforcementService(paths, taskGraphService, artifactRepository));
    }
}
