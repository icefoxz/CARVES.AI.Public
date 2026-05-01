using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeRepoAuthoredGateLoop()
    {
        return OperatorSurfaceFormatter.RuntimeRepoAuthoredGateLoop(CreateRuntimeRepoAuthoredGateLoopService().Build());
    }

    public OperatorCommandResult ApiRuntimeRepoAuthoredGateLoop()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeRepoAuthoredGateLoopService().Build()));
    }

    private RuntimeRepoAuthoredGateLoopService CreateRuntimeRepoAuthoredGateLoopService()
    {
        return new RuntimeRepoAuthoredGateLoopService(repoRoot, paths);
    }
}
