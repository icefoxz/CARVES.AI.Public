using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeSessionGatewayRepeatability()
    {
        return OperatorSurfaceFormatter.RuntimeSessionGatewayRepeatability(
            CreateRuntimeSessionGatewayRepeatabilityService().Build());
    }

    public OperatorCommandResult ApiRuntimeSessionGatewayRepeatability()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeSessionGatewayRepeatabilityService().Build()));
    }

    private RuntimeSessionGatewayRepeatabilityService CreateRuntimeSessionGatewayRepeatabilityService()
    {
        return new RuntimeSessionGatewayRepeatabilityService(
            repoRoot,
            () => CreateRuntimeSessionGatewayPrivateAlphaHandoffService().Build(),
            taskGraphService,
            artifactRepository,
            operatorOsEventStreamService,
            new ReviewEvidenceProjectionService(reviewEvidenceGateService, reviewWritebackService));
    }
}
