using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentOperatorFeedbackClosure()
    {
        return OperatorSurfaceFormatter.RuntimeAgentOperatorFeedbackClosure(CreateRuntimeAgentOperatorFeedbackClosureService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentOperatorFeedbackClosure()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentOperatorFeedbackClosureService().Build()));
    }

    private RuntimeAgentOperatorFeedbackClosureService CreateRuntimeAgentOperatorFeedbackClosureService()
    {
        return new RuntimeAgentOperatorFeedbackClosureService(repoRoot);
    }
}
