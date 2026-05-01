using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeGitNativeCodingLoop()
    {
        return OperatorSurfaceFormatter.RuntimeGitNativeCodingLoop(CreateRuntimeGitNativeCodingLoopService().Build());
    }

    public OperatorCommandResult ApiRuntimeGitNativeCodingLoop()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeGitNativeCodingLoopService().Build()));
    }

    private RuntimeGitNativeCodingLoopService CreateRuntimeGitNativeCodingLoopService()
    {
        return new RuntimeGitNativeCodingLoopService(repoRoot, paths);
    }
}
