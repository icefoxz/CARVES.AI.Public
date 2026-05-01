using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectExecutionHardening(string taskId)
    {
        return OperatorSurfaceFormatter.ExecutionHardening(CreateExecutionHardeningSurfaceService().Build(taskId));
    }

    public OperatorCommandResult ApiExecutionHardening(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateExecutionHardeningSurfaceService().Build(taskId)));
    }

    private ExecutionHardeningSurfaceService CreateExecutionHardeningSurfaceService()
    {
        return new ExecutionHardeningSurfaceService(
            repoRoot,
            paths,
            taskGraphService,
            artifactRepository,
            executionPacketCompilerService,
            new PacketEnforcementService(paths, taskGraphService, artifactRepository),
            controlPlaneLockService);
    }
}
