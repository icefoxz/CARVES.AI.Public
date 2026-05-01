using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeCodeUnderstandingEngine()
    {
        return OperatorSurfaceFormatter.RuntimeCodeUnderstandingEngine(CreateRuntimeCodeUnderstandingEngineService().Build());
    }

    public OperatorCommandResult ApiRuntimeCodeUnderstandingEngine()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeCodeUnderstandingEngineService().Build()));
    }

    private RuntimeCodeUnderstandingEngineService CreateRuntimeCodeUnderstandingEngineService()
    {
        return new RuntimeCodeUnderstandingEngineService(repoRoot, paths);
    }
}
