using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeGovernanceSurfaceCoverageAudit()
    {
        return OperatorSurfaceFormatter.RuntimeGovernanceSurfaceCoverageAudit(BuildRuntimeGovernanceSurfaceCoverageAudit());
    }

    public OperatorCommandResult ApiRuntimeGovernanceSurfaceCoverageAudit()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildRuntimeGovernanceSurfaceCoverageAudit()));
    }

    private RuntimeGovernanceSurfaceCoverageAuditSurface BuildRuntimeGovernanceSurfaceCoverageAudit()
    {
        return new RuntimeGovernanceSurfaceCoverageAuditService(repoRoot)
            .Build(RuntimeSurfaceCommandRegistry.CommandNames, CreateRuntimeExternalConsumerResourcePackService().Build());
    }
}
