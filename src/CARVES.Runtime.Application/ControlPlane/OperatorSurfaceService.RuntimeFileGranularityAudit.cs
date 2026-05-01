using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeFileGranularityAudit()
    {
        return OperatorSurfaceFormatter.RuntimeFileGranularityAudit(BuildRuntimeFileGranularityAudit());
    }

    public OperatorCommandResult ApiRuntimeFileGranularityAudit()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildRuntimeFileGranularityAudit()));
    }

    private RuntimeFileGranularityAuditSurface BuildRuntimeFileGranularityAudit()
    {
        return new RuntimeFileGranularityAuditService(repoRoot).Build();
    }
}
