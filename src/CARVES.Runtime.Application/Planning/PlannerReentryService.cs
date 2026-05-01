using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerReentryService
{
    private readonly PlannerHostService plannerHostService;

    public PlannerReentryService(PlannerHostService plannerHostService)
    {
        this.plannerHostService = plannerHostService;
    }

    public PlannerReentryResult Reenter(RuntimeSessionState session)
    {
        return plannerHostService.RunOnce(
            session,
            PlannerWakeReason.ExecutionBacklogCleared,
            "execution backlog cleared and planner re-entry is justified").Reentry;
    }
}
