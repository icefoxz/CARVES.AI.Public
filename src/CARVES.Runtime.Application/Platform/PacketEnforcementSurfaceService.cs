using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class PacketEnforcementSurfaceService
{
    private readonly PacketEnforcementService packetEnforcementService;

    public PacketEnforcementSurfaceService(PacketEnforcementService packetEnforcementService)
    {
        this.packetEnforcementService = packetEnforcementService;
    }

    public PacketEnforcementSurfaceSnapshot Build(string taskId)
    {
        return packetEnforcementService.BuildSnapshot(taskId);
    }
}
