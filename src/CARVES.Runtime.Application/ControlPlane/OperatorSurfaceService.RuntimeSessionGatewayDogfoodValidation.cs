using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayDogfoodValidation()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayDogfoodValidation(
            CreateRuntimeSessionGatewayDogfoodValidationService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayDogfoodValidation()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayDogfoodValidationService().Build()));
    }

    private RuntimeSessionGatewayDogfoodValidationService CreateRuntimeSessionGatewayDogfoodValidationService()
    {
        return new RuntimeSessionGatewayDogfoodValidationService(
            repoRoot,
            () => CreateRuntimeGovernanceProgramReauditService().Build());
    }
}
