using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentBootstrapPacket()
    {
        return OperatorSurfaceFormatter.RuntimeAgentBootstrapPacket(CreateRuntimeAgentBootstrapPacketService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentBootstrapPacket()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentBootstrapPacketService().Build()));
    }

    public OperatorCommandResult InspectRuntimeAgentBootstrapReceipt(string? priorReceiptPath = null)
    {
        return OperatorSurfaceFormatter.RuntimeAgentBootstrapReceipt(CreateRuntimeAgentBootstrapReceiptService().Build(priorReceiptPath));
    }

    public OperatorCommandResult ApiRuntimeAgentBootstrapReceipt(string? priorReceiptPath = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentBootstrapReceiptService().Build(priorReceiptPath)));
    }

    public OperatorCommandResult InspectRuntimeAgentQueueProjection()
    {
        return OperatorSurfaceFormatter.RuntimeAgentQueueProjection(CreateRuntimeAgentQueueProjectionService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentQueueProjection()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentQueueProjectionService().Build()));
    }

    public OperatorCommandResult InspectRuntimeAgentTaskOverlay(string taskId)
    {
        return OperatorSurfaceFormatter.RuntimeAgentTaskOverlay(CreateRuntimeAgentTaskBootstrapOverlayService().Build(taskId));
    }

    public OperatorCommandResult ApiRuntimeAgentTaskOverlay(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentTaskBootstrapOverlayService().Build(taskId)));
    }

    public OperatorCommandResult InspectRuntimeAgentModelProfileRouting()
    {
        return OperatorSurfaceFormatter.RuntimeAgentModelProfileRouting(CreateRuntimeAgentModelProfileRoutingService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentModelProfileRouting()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentModelProfileRoutingService().Build()));
    }

    public OperatorCommandResult InspectRuntimeAgentLoopStallGuard(string taskId)
    {
        return OperatorSurfaceFormatter.RuntimeAgentLoopStallGuard(CreateRuntimeAgentLoopStallGuardService().Build(taskId));
    }

    public OperatorCommandResult ApiRuntimeAgentLoopStallGuard(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentLoopStallGuardService().Build(taskId)));
    }

    public OperatorCommandResult InspectRuntimeAgentWeakModelLane()
    {
        return OperatorSurfaceFormatter.RuntimeWeakModelExecutionLane(CreateRuntimeWeakModelExecutionLaneService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentWeakModelLane()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeWeakModelExecutionLaneService().Build()));
    }

    private RuntimeAgentBootstrapPacketService CreateRuntimeAgentBootstrapPacketService()
    {
        return new RuntimeAgentBootstrapPacketService(repoRoot, paths, taskGraphService);
    }

    private RuntimeAgentTaskBootstrapOverlayService CreateRuntimeAgentTaskBootstrapOverlayService()
    {
        return new RuntimeAgentTaskBootstrapOverlayService(repoRoot, paths, taskGraphService, executionPacketCompilerService, gitClient);
    }

    private RuntimeAgentBootstrapReceiptService CreateRuntimeAgentBootstrapReceiptService()
    {
        return new RuntimeAgentBootstrapReceiptService(repoRoot, paths, taskGraphService);
    }

    private RuntimeAgentQueueProjectionService CreateRuntimeAgentQueueProjectionService()
    {
        return new RuntimeAgentQueueProjectionService(repoRoot, paths, taskGraphService);
    }

    private RuntimeAgentModelProfileRoutingService CreateRuntimeAgentModelProfileRoutingService()
    {
        return new RuntimeAgentModelProfileRoutingService(repoRoot, paths, currentModelQualificationService);
    }

    private RuntimeAgentLoopStallGuardService CreateRuntimeAgentLoopStallGuardService()
    {
        return new RuntimeAgentLoopStallGuardService(repoRoot, paths, taskGraphService);
    }

    private RuntimeWeakModelExecutionLaneService CreateRuntimeWeakModelExecutionLaneService()
    {
        return new RuntimeWeakModelExecutionLaneService(repoRoot, paths, currentModelQualificationService);
    }
}
