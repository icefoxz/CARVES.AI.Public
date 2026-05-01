using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeExternalTargetPilotStart()
    {
        return OperatorSurfaceFormatter.RuntimeExternalTargetPilotStart(CreateRuntimeExternalTargetPilotStartService().BuildStart());
    }

    public OperatorCommandResult ApiRuntimeExternalTargetPilotStart()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeExternalTargetPilotStartService().BuildStart()));
    }

    public OperatorCommandResult InspectRuntimeExternalTargetPilotNext()
    {
        return OperatorSurfaceFormatter.RuntimeExternalTargetPilotNext(CreateRuntimeExternalTargetPilotStartService().BuildNext());
    }

    public OperatorCommandResult ApiRuntimeExternalTargetPilotNext()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeExternalTargetPilotStartService().BuildNext()));
    }

    private RuntimeExternalTargetPilotStartService CreateRuntimeExternalTargetPilotStartService()
    {
        return new RuntimeExternalTargetPilotStartService(
            repoRoot,
            () => CreateRuntimeAlphaExternalUseReadinessService().Build(),
            () => CreateRuntimeCliInvocationContractService().Build(),
            () => CreateRuntimeExternalConsumerResourcePackService().Build(),
            () => CreateRuntimeGovernedAgentHandoffProofService().Build(),
            () => CreateRuntimeProductClosurePilotStatusService().Build());
    }
}
