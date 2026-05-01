using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.TaskGraph;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.ControlPlane;

public interface IMarkdownSyncService
{
    void Sync(
        DomainTaskGraph graph,
        TaskNode? currentTask = null,
        TaskRunReport? report = null,
        PlannerReview? review = null,
        RuntimeSessionState? session = null,
        TaskScheduleDecision? schedulerDecision = null);
}
