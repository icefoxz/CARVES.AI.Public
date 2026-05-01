using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemFollowUpCandidates()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemFollowUpCandidates(CreateRuntimeAgentProblemFollowUpCandidatesService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemFollowUpCandidates()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemFollowUpCandidatesService().Build()));
    }

    private RuntimeAgentProblemFollowUpCandidatesService CreateRuntimeAgentProblemFollowUpCandidatesService()
    {
        return new RuntimeAgentProblemFollowUpCandidatesService(
            repoRoot,
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
