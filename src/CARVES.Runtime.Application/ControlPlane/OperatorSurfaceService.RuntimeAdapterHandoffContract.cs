using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAdapterHandoffContract()
    {
        return OperatorSurfaceFormatter.RuntimeAdapterHandoffContract(CreateRuntimeAdapterHandoffContractService().Build());
    }

    public OperatorCommandResult ApiRuntimeAdapterHandoffContract()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAdapterHandoffContractService().Build()));
    }

    private RuntimeAdapterHandoffContractService CreateRuntimeAdapterHandoffContractService()
    {
        return new RuntimeAdapterHandoffContractService(repoRoot);
    }
}
