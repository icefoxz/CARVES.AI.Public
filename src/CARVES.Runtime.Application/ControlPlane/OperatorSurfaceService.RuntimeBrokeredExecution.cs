using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeBrokeredExecution(string taskId)
    {
        return OperatorSurfaceFormatter.RuntimeBrokeredExecution(CreateRuntimeBrokeredExecutionSurfaceService().Build(taskId));
    }

    public OperatorCommandResult ApiRuntimeBrokeredExecution(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeBrokeredExecutionSurfaceService().Build(taskId)));
    }

    private RuntimeBrokeredExecutionSurfaceService CreateRuntimeBrokeredExecutionSurfaceService()
    {
        var memoryPatternWritebackRouteAuthorizationService = new MemoryPatternWritebackRouteAuthorizationService(repoRoot);
        return new RuntimeBrokeredExecutionSurfaceService(
            repoRoot,
            taskGraphService,
            executionPacketCompilerService,
            new PacketEnforcementService(paths, taskGraphService, artifactRepository),
            artifactRepository,
            reviewEvidenceGateService,
            CreateRuntimeWorkspaceMutationAuditService(),
            memoryPatternWritebackRouteAuthorizationService);
    }
}
