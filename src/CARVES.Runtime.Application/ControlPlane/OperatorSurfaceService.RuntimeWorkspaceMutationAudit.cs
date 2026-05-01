using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeWorkspaceMutationAudit(string taskId)
    {
        return OperatorSurfaceFormatter.RuntimeWorkspaceMutationAudit(CreateRuntimeWorkspaceMutationAuditService().Build(taskId));
    }

    public OperatorCommandResult ApiRuntimeWorkspaceMutationAudit(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeWorkspaceMutationAuditService().Build(taskId)));
    }

    private RuntimeWorkspaceMutationAuditService CreateRuntimeWorkspaceMutationAuditService()
    {
        return new RuntimeWorkspaceMutationAuditService(
            repoRoot,
            taskGraphService,
            artifactRepository,
            managedWorkspaceLeaseService);
    }
}
