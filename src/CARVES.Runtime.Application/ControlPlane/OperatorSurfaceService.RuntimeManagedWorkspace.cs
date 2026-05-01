using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeManagedWorkspace()
    {
        return OperatorSurfaceFormatter.RuntimeManagedWorkspace(managedWorkspaceLeaseService.BuildSurface());
    }

    public OperatorCommandResult ApiRuntimeManagedWorkspace()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(managedWorkspaceLeaseService.BuildSurface()));
    }
}
