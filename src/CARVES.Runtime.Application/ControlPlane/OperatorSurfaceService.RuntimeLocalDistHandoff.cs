using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeLocalDistHandoff()
    {
        return OperatorSurfaceFormatter.RuntimeLocalDistHandoff(CreateRuntimeLocalDistHandoffService().Build());
    }

    public OperatorCommandResult ApiRuntimeLocalDistHandoff()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeLocalDistHandoffService().Build()));
    }

    private RuntimeLocalDistHandoffService CreateRuntimeLocalDistHandoffService()
    {
        return new RuntimeLocalDistHandoffService(repoRoot);
    }
}
