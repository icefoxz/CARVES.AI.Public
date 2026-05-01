using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentThreadStart()
    {
        return OperatorSurfaceFormatter.RuntimeAgentThreadStart(CreateRuntimeAgentThreadStartService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentThreadStart()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentThreadStartService().Build()));
    }

    private RuntimeAgentThreadStartService CreateRuntimeAgentThreadStartService()
    {
        return new RuntimeAgentThreadStartService(
            repoRoot,
            () => CreateRuntimeExternalTargetPilotStartService().BuildStart(),
            () => CreateRuntimeAgentProblemFollowUpPlanningGateService().Build(),
            () => CreateRuntimeProductClosurePilotStatusService().Build(),
            () => CreateRuntimeGovernedAgentHandoffProofService().Build());
    }
}
