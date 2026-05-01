using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeLocalDistFreshnessSmoke()
    {
        return OperatorSurfaceFormatter.RuntimeLocalDistFreshnessSmoke(CreateRuntimeLocalDistFreshnessSmokeService().Build());
    }

    public OperatorCommandResult ApiRuntimeLocalDistFreshnessSmoke()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeLocalDistFreshnessSmokeService().Build()));
    }

    private RuntimeLocalDistFreshnessSmokeService CreateRuntimeLocalDistFreshnessSmokeService()
    {
        return new RuntimeLocalDistFreshnessSmokeService(repoRoot);
    }
}
