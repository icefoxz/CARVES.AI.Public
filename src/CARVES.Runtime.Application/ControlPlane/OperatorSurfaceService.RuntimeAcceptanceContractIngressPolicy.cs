using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAcceptanceContractIngressPolicy()
    {
        return OperatorSurfaceFormatter.RuntimeAcceptanceContractIngressPolicy(CreateRuntimeAcceptanceContractIngressPolicyService().Build());
    }

    public OperatorCommandResult ApiRuntimeAcceptanceContractIngressPolicy()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAcceptanceContractIngressPolicyService().Build()));
    }

    private RuntimeAcceptanceContractIngressPolicyService CreateRuntimeAcceptanceContractIngressPolicyService()
    {
        return new RuntimeAcceptanceContractIngressPolicyService(repoRoot);
    }
}
