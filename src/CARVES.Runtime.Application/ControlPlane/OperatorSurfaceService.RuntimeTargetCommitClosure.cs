using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetCommitClosure()
    {
        return OperatorSurfaceFormatter.RuntimeTargetCommitClosure(CreateRuntimeTargetCommitClosureService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetCommitClosure()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetCommitClosureService().Build()));
    }

    private RuntimeTargetCommitClosureService CreateRuntimeTargetCommitClosureService()
    {
        return new RuntimeTargetCommitClosureService(repoRoot);
    }
}
