using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeProductPilotProof()
    {
        return OperatorSurfaceFormatter.RuntimeProductPilotProof(CreateRuntimeProductPilotProofService().Build());
    }

    public OperatorCommandResult ApiRuntimeProductPilotProof()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeProductPilotProofService().Build()));
    }

    private RuntimeProductPilotProofService CreateRuntimeProductPilotProofService()
    {
        return new RuntimeProductPilotProofService(repoRoot);
    }
}
