using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeExportProfiles(string? profileId = null)
    {
        return OperatorSurfaceFormatter.RuntimeExportProfiles(CreateRuntimeExportProfileService().BuildSurface(profileId));
    }

    public OperatorCommandResult ApiRuntimeExportProfiles(string? profileId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeExportProfileService().BuildSurface(profileId)));
    }
}
