using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAlphaExternalUseReadiness()
    {
        return OperatorSurfaceFormatter.RuntimeAlphaExternalUseReadiness(
            CreateRuntimeAlphaExternalUseReadinessService().Build());
    }

    public OperatorCommandResult ApiRuntimeAlphaExternalUseReadiness()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeAlphaExternalUseReadinessService().Build()));
    }

    private RuntimeAlphaExternalUseReadinessService CreateRuntimeAlphaExternalUseReadinessService()
    {
        return new RuntimeAlphaExternalUseReadinessService(
            repoRoot,
            () => CreateRuntimeLocalDistFreshnessSmokeService().Build(),
            () => CreateRuntimeExternalConsumerResourcePackService().Build(),
            () => CreateRuntimeGovernedAgentHandoffProofService().Build(),
            () => CreateRuntimeProductClosurePilotGuideService().Build(),
            () => CreateRuntimeSessionGatewayPrivateAlphaHandoffService().Build(),
            () => CreateRuntimeSessionGatewayRepeatabilityService().Build());
    }
}
