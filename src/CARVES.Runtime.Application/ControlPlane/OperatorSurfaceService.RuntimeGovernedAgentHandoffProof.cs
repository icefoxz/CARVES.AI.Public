using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeGovernedAgentHandoffProof()
    {
        return OperatorSurfaceFormatter.RuntimeGovernedAgentHandoffProof(CreateRuntimeGovernedAgentHandoffProofService().Build());
    }

    public OperatorCommandResult ApiRuntimeGovernedAgentHandoffProof()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeGovernedAgentHandoffProofService().Build()));
    }

    private RuntimeGovernedAgentHandoffProofService CreateRuntimeGovernedAgentHandoffProofService()
    {
        return new RuntimeGovernedAgentHandoffProofService(
            repoRoot,
            () => platformDashboardService.BuildAgentWorkingModesSurface(),
            () => CreateRuntimeAdapterHandoffContractService().Build(),
            () => CreateRuntimeProtectedTruthRootPolicyService().Build());
    }
}
