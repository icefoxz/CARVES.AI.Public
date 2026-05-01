using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeCliInvocationContract()
    {
        return OperatorSurfaceFormatter.RuntimeCliInvocationContract(CreateRuntimeCliInvocationContractService().Build());
    }

    public OperatorCommandResult ApiRuntimeCliInvocationContract()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeCliInvocationContractService().Build()));
    }

    private RuntimeCliInvocationContractService CreateRuntimeCliInvocationContractService()
    {
        return new RuntimeCliInvocationContractService(repoRoot);
    }
}
