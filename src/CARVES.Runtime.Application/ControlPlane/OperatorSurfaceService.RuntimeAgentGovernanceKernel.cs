using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentGovernanceKernel()
    {
        return OperatorSurfaceFormatter.RuntimeAgentGovernanceKernel(CreateRuntimeAgentGovernanceKernelService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentGovernanceKernel()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentGovernanceKernelService().Build()));
    }

    private RuntimeAgentGovernanceKernelService CreateRuntimeAgentGovernanceKernelService()
    {
        return new RuntimeAgentGovernanceKernelService(repoRoot, paths);
    }
}
