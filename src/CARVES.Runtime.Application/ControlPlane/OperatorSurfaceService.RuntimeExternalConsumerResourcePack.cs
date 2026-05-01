using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeExternalConsumerResourcePack()
    {
        return OperatorSurfaceFormatter.RuntimeExternalConsumerResourcePack(CreateRuntimeExternalConsumerResourcePackService().Build());
    }

    public OperatorCommandResult ApiRuntimeExternalConsumerResourcePack()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeExternalConsumerResourcePackService().Build()));
    }

    private RuntimeExternalConsumerResourcePackService CreateRuntimeExternalConsumerResourcePackService()
    {
        return new RuntimeExternalConsumerResourcePackService(repoRoot);
    }
}
