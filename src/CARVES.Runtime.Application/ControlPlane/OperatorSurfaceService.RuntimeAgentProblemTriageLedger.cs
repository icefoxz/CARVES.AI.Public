using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemTriageLedger()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemTriageLedger(CreateRuntimeAgentProblemTriageLedgerService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemTriageLedger()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemTriageLedgerService().Build()));
    }

    private RuntimeAgentProblemTriageLedgerService CreateRuntimeAgentProblemTriageLedgerService()
    {
        return new RuntimeAgentProblemTriageLedgerService(
            repoRoot,
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
