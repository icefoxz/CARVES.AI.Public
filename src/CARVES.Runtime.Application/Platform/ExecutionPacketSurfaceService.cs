using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ExecutionPacketSurfaceService
{
    private readonly ExecutionPacketCompilerService executionPacketCompilerService;

    public ExecutionPacketSurfaceService(ExecutionPacketCompilerService executionPacketCompilerService)
    {
        this.executionPacketCompilerService = executionPacketCompilerService;
    }

    public ExecutionPacketSurfaceSnapshot Build(string taskId)
    {
        return executionPacketCompilerService.BuildSnapshot(taskId);
    }
}
