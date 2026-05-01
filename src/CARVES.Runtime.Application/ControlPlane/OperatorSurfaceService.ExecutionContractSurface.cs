using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectExecutionContractSurface()
    {
        return OperatorSurfaceFormatter.ExecutionContractSurface(CreateExecutionContractSurfaceService().Build());
    }

    public OperatorCommandResult ApiExecutionContractSurface()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateExecutionContractSurfaceService().Build()));
    }

    private ExecutionContractSurfaceService CreateExecutionContractSurfaceService()
    {
        return new ExecutionContractSurfaceService();
    }
}
