using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeMarkdownReadPathBudget(string? taskId = null)
    {
        return OperatorSurfaceFormatter.RuntimeMarkdownReadPathBudget(BuildRuntimeMarkdownReadPathBudget(taskId));
    }

    public OperatorCommandResult ApiRuntimeMarkdownReadPathBudget(string? taskId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildRuntimeMarkdownReadPathBudget(taskId)));
    }

    private RuntimeMarkdownReadPathBudgetSurface BuildRuntimeMarkdownReadPathBudget(string? taskId)
    {
        var bootstrap = CreateRuntimeAgentBootstrapPacketService().Build();
        RuntimeAgentTaskBootstrapOverlaySurface? overlay = null;
        var resolvedTaskId = ResolveMarkdownBudgetTaskId(taskId, bootstrap);
        if (!IsNoTask(resolvedTaskId))
        {
            try
            {
                overlay = CreateRuntimeAgentTaskBootstrapOverlayService().Build(resolvedTaskId);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return new RuntimeMarkdownReadPathBudgetService(repoRoot, paths).Build(bootstrap, overlay, taskId);
    }

    private static string ResolveMarkdownBudgetTaskId(string? requestedTaskId, RuntimeAgentBootstrapPacketSurface bootstrap)
    {
        if (!string.IsNullOrWhiteSpace(requestedTaskId))
        {
            return requestedTaskId;
        }

        var currentTaskId = bootstrap.Packet.HotPathContext.CurrentTaskId;
        if (!IsNoTask(currentTaskId))
        {
            return currentTaskId;
        }

        var activeTask = bootstrap.Packet.HotPathContext.ActiveTasks.FirstOrDefault();
        return activeTask?.TaskId ?? "N/A";
    }
}
