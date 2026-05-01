using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayPrivateAlphaHandoff()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayPrivateAlphaHandoff(
            CreateRuntimeSessionGatewayPrivateAlphaHandoffService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayPrivateAlphaHandoff()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayPrivateAlphaHandoffService().Build()));
    }

    private RuntimeSessionGatewayPrivateAlphaHandoffService CreateRuntimeSessionGatewayPrivateAlphaHandoffService()
    {
        return new RuntimeSessionGatewayPrivateAlphaHandoffService(
            repoRoot,
            () => CreateRuntimeSessionGatewayDogfoodValidationService().Build(),
            () => operationalSummaryService.Build(refreshProviderHealth: false),
            () => CreateRuntimeHealthCheckService().Evaluate());
    }
}
