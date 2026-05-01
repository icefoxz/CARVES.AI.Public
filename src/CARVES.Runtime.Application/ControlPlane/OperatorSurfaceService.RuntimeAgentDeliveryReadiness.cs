using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentDeliveryReadiness()
    {
        return OperatorSurfaceFormatter.RuntimeAgentDeliveryReadiness(CreateRuntimeAgentDeliveryReadinessService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentDeliveryReadiness()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentDeliveryReadinessService().Build()));
    }

    private RuntimeAgentDeliveryReadinessService CreateRuntimeAgentDeliveryReadinessService()
    {
        return new RuntimeAgentDeliveryReadinessService(repoRoot);
    }
}
