using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetAgentBootstrapPack(bool writeRequested = false)
    {
        return OperatorSurfaceFormatter.RuntimeTargetAgentBootstrapPack(CreateRuntimeTargetAgentBootstrapPackService().Build(writeRequested));
    }

    public OperatorCommandResult ApiRuntimeTargetAgentBootstrapPack(bool writeRequested = false)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetAgentBootstrapPackService().Build(writeRequested)));
    }

    private RuntimeTargetAgentBootstrapPackService CreateRuntimeTargetAgentBootstrapPackService()
    {
        return new RuntimeTargetAgentBootstrapPackService(repoRoot);
    }
}
