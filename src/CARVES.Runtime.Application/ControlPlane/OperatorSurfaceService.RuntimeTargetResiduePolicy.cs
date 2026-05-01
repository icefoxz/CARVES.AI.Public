using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetResiduePolicy()
    {
        return OperatorSurfaceFormatter.RuntimeTargetResiduePolicy(CreateRuntimeTargetResiduePolicyService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetResiduePolicy()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetResiduePolicyService().Build()));
    }

    private RuntimeTargetResiduePolicyService CreateRuntimeTargetResiduePolicyService()
    {
        return new RuntimeTargetResiduePolicyService(repoRoot);
    }
}
