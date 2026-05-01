using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectExecutionPacket(string taskId)
    {
        return OperatorSurfaceFormatter.ExecutionPacket(CreateExecutionPacketSurfaceService().Build(taskId));
    }

    public OperatorCommandResult ApiExecutionPacket(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateExecutionPacketSurfaceService().Build(taskId)));
    }

    private ExecutionPacketSurfaceService CreateExecutionPacketSurfaceService()
    {
        return new ExecutionPacketSurfaceService(executionPacketCompilerService);
    }
}
