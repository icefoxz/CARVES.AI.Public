using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentValidationBundle()
    {
        return OperatorSurfaceFormatter.RuntimeAgentValidationBundle(CreateRuntimeAgentValidationBundleService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentValidationBundle()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentValidationBundleService().Build()));
    }

    private RuntimeAgentValidationBundleService CreateRuntimeAgentValidationBundleService()
    {
        return new RuntimeAgentValidationBundleService(repoRoot);
    }
}
