using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Infrastructure.ControlPlane;

public sealed class MarkdownSyncService : IMarkdownSyncService
{
    private static readonly string[] ProjectionTargets = ["TASK_QUEUE.md", "STATE.md", "CURRENT_TASK.md"];
    private readonly ControlPlanePaths paths;
    private readonly MarkdownProjector projector;
    private readonly IControlPlaneLockService lockService;
    private readonly MarkdownProjectionHealthService projectionHealthService;

    public MarkdownSyncService(ControlPlanePaths paths, MarkdownProjector projector, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.projector = projector;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
        projectionHealthService = new MarkdownProjectionHealthService(paths);
    }

    public void Sync(
        TaskGraph graph,
        TaskNode? currentTask = null,
        TaskRunReport? report = null,
        PlannerReview? review = null,
        RuntimeSessionState? session = null,
        TaskScheduleDecision? schedulerDecision = null)
    {
        var projection = projector.Build(graph, currentTask, report, review, session, schedulerDecision);
        try
        {
            using var _ = lockService.Acquire("markdown-projection");
            AtomicFileWriter.WriteAllTextIfChanged(Path.Combine(paths.AiRoot, "TASK_QUEUE.md"), projection.TaskQueue);
            AtomicFileWriter.WriteAllTextIfChanged(Path.Combine(paths.AiRoot, "STATE.md"), projection.State);
            AtomicFileWriter.WriteAllTextIfChanged(Path.Combine(paths.AiRoot, "CURRENT_TASK.md"), projection.CurrentTask);
            projectionHealthService.RecordSuccess();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            projectionHealthService.RecordFailure(exception, ProjectionTargets);
        }
    }
}
