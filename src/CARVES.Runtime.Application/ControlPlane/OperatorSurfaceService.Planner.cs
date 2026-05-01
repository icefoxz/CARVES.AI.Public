using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult PlannerStatus()
    {
        return OperatorSurfaceFormatter.PlannerStatus(devLoopService.GetSession());
    }

    public OperatorCommandResult PlannerRun(bool dryRun, PlannerWakeReason wakeReason, string detail)
    {
        return OperatorSurfaceFormatter.PlannerRun(devLoopService.RunPlanner(dryRun, wakeReason, detail));
    }

    public OperatorCommandResult PlannerLoop(bool dryRun, int iterations, PlannerWakeReason wakeReason, string detail)
    {
        return OperatorSurfaceFormatter.PlannerLoop(devLoopService.RunPlannerLoop(dryRun, iterations, wakeReason, detail));
    }

    public OperatorCommandResult PlannerWake(PlannerWakeReason wakeReason, string detail)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            detail,
            OwnershipScope.PlannerControl,
            "planner",
            operationClass: "planner",
            operation: "wake");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.PlannerControl,
            "planner",
            detail,
            () => OperatorSurfaceFormatter.PlannerLifecycleChanged("Woke", devLoopService.WakePlanner(wakeReason, detail)));
    }

    public OperatorCommandResult PlannerSleep(PlannerSleepReason sleepReason, string detail)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            detail,
            OwnershipScope.PlannerControl,
            "planner",
            operationClass: "planner",
            operation: "sleep");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.PlannerControl,
            "planner",
            detail,
            () => OperatorSurfaceFormatter.PlannerLifecycleChanged("Slept", devLoopService.SleepPlanner(sleepReason, detail)));
    }
}
