using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayGovernanceAssist()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayGovernanceAssist(
            CreateRuntimeSessionGatewayGovernanceAssistService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayGovernanceAssist()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayGovernanceAssistService().Build()));
    }

    private RuntimeSessionGatewayGovernanceAssistService CreateRuntimeSessionGatewayGovernanceAssistService()
    {
        return new RuntimeSessionGatewayGovernanceAssistService(
            repoRoot,
            () => CreateRuntimeSessionGatewayRepeatabilityService().Build());
    }
}
