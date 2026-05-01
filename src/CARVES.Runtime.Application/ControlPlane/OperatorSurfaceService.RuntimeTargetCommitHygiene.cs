using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetCommitHygiene()
    {
        return OperatorSurfaceFormatter.RuntimeTargetCommitHygiene(CreateRuntimeTargetCommitHygieneService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetCommitHygiene()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetCommitHygieneService().Build()));
    }

    private RuntimeTargetCommitHygieneService CreateRuntimeTargetCommitHygieneService()
    {
        return new RuntimeTargetCommitHygieneService(repoRoot);
    }
}
