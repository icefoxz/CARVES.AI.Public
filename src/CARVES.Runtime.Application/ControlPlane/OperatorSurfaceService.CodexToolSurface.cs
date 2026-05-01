using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectCodexToolSurface()
    {
        return OperatorSurfaceFormatter.CodexToolSurface(CreateCodexToolSurfaceService().Build());
    }

    public OperatorCommandResult ApiCodexToolSurface()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateCodexToolSurfaceService().Build()));
    }

    private CodexToolSurfaceService CreateCodexToolSurfaceService()
    {
        return new CodexToolSurfaceService();
    }
}
