using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentFailureRecoveryClosure()
    {
        return OperatorSurfaceFormatter.RuntimeAgentFailureRecoveryClosure(CreateRuntimeAgentFailureRecoveryClosureService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentFailureRecoveryClosure()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentFailureRecoveryClosureService().Build()));
    }

    private RuntimeAgentFailureRecoveryClosureService CreateRuntimeAgentFailureRecoveryClosureService()
    {
        return new RuntimeAgentFailureRecoveryClosureService(repoRoot);
    }
}
