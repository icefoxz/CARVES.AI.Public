using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class CodexToolSurfaceServiceTests
{
    [Fact]
    public void Build_ProjectsExpectedMinimalCodexToolRegistry()
    {
        var service = new CodexToolSurfaceService();

        var snapshot = service.Build();

        Assert.Equal("codex-tool-surface", snapshot.SurfaceId);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "get_task" && tool.Availability == CodexToolAvailability.Available);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "review_task" && tool.ActionClass == CodexToolActionClass.PlannerOnly);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "submit_result" && tool.TruthAffecting);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "get_execution_packet" && tool.Availability == CodexToolAvailability.Available);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "load_memory_bundle" && tool.Availability == CodexToolAvailability.Available);
        Assert.Contains(snapshot.Tools, tool => tool.ToolId == "read_code" && tool.ActionClass == CodexToolActionClass.LocalEphemeral);
    }

    [Fact]
    public void Build_KeepsPlannerOnlyActionsSeparatedFromWorkerAllowedActions()
    {
        var service = new CodexToolSurfaceService();

        var snapshot = service.Build();

        var plannerOnly = snapshot.Tools.Where(tool => tool.ActionClass == CodexToolActionClass.PlannerOnly).Select(tool => tool.ToolId).ToArray();
        var workerAllowed = snapshot.Tools.Where(tool => tool.ActionClass == CodexToolActionClass.WorkerAllowed).Select(tool => tool.ToolId).ToArray();

        Assert.Contains("review_task", plannerOnly);
        Assert.Contains("sync_state", plannerOnly);
        Assert.Contains("audit_runtime", plannerOnly);
        Assert.DoesNotContain("review_task", workerAllowed);
        Assert.Contains("get_task", workerAllowed);
        Assert.Contains("submit_result", workerAllowed);
    }
}
