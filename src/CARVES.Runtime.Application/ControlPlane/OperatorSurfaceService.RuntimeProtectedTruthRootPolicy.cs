using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeProtectedTruthRootPolicy()
    {
        return OperatorSurfaceFormatter.RuntimeProtectedTruthRootPolicy(CreateRuntimeProtectedTruthRootPolicyService().Build());
    }

    public OperatorCommandResult ApiRuntimeProtectedTruthRootPolicy()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeProtectedTruthRootPolicyService().Build()));
    }

    private RuntimeProtectedTruthRootPolicyService CreateRuntimeProtectedTruthRootPolicyService()
    {
        return new RuntimeProtectedTruthRootPolicyService(repoRoot);
    }
}
