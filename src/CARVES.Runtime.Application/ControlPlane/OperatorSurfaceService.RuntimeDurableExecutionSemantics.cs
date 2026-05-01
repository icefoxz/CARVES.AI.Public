using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeDurableExecutionSemantics()
    {
        return OperatorSurfaceFormatter.RuntimeDurableExecutionSemantics(CreateRuntimeDurableExecutionSemanticsService().Build());
    }

    public OperatorCommandResult ApiRuntimeDurableExecutionSemantics()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeDurableExecutionSemanticsService().Build()));
    }

    private RuntimeDurableExecutionSemanticsService CreateRuntimeDurableExecutionSemanticsService()
    {
        return new RuntimeDurableExecutionSemanticsService(repoRoot, paths);
    }
}
