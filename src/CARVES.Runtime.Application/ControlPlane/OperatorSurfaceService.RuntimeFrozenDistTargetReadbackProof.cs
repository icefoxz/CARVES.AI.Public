using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeFrozenDistTargetReadbackProof()
    {
        return OperatorSurfaceFormatter.RuntimeFrozenDistTargetReadbackProof(CreateRuntimeFrozenDistTargetReadbackProofService().Build());
    }

    public OperatorCommandResult ApiRuntimeFrozenDistTargetReadbackProof()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeFrozenDistTargetReadbackProofService().Build()));
    }

    private RuntimeFrozenDistTargetReadbackProofService CreateRuntimeFrozenDistTargetReadbackProofService()
    {
        return new RuntimeFrozenDistTargetReadbackProofService(repoRoot);
    }
}
