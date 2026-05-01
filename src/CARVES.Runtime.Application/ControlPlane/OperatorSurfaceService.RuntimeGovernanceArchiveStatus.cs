using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeGovernanceArchiveStatus()
    {
        return OperatorSurfaceFormatter.RuntimeGovernanceArchiveStatus(
            CreateRuntimeGovernanceArchiveStatusService().Build());
    }

    public OperatorCommandResult ApiRuntimeGovernanceArchiveStatus()
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeGovernanceArchiveStatusService().Build()));
    }

    public OperatorCommandResult InspectRuntimeGovernanceArchiveStatusAlias(string surfaceId, string? legacyArgument = null)
    {
        return OperatorSurfaceFormatter.RuntimeGovernanceArchiveStatus(
            CreateRuntimeGovernanceArchiveStatusService().Build(surfaceId, legacyArgument));
    }

    public OperatorCommandResult ApiRuntimeGovernanceArchiveStatusAlias(string surfaceId, string? legacyArgument = null)
    {
        return OperatorCommandResult.Success(
            operatorApiService.ToJson(CreateRuntimeGovernanceArchiveStatusService().Build(surfaceId, legacyArgument)));
    }

    private RuntimeGovernanceArchiveStatusService CreateRuntimeGovernanceArchiveStatusService()
    {
        return new RuntimeGovernanceArchiveStatusService(repoRoot);
    }
}
