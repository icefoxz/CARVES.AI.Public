using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerIntentRoutingService
{
    public PlannerIntent Classify(PlannerWakeReason wakeReason)
    {
        return wakeReason switch
        {
            PlannerWakeReason.NewGoalArrived => PlannerIntent.Planning,
            PlannerWakeReason.NewCardArrived => PlannerIntent.Planning,
            PlannerWakeReason.OpportunityDeltaDetected => PlannerIntent.Planning,
            PlannerWakeReason.ExplicitHumanWake => PlannerIntent.Planning,
            PlannerWakeReason.ExecutionBacklogCleared => PlannerIntent.Execution,
            PlannerWakeReason.DependencyUnlocked => PlannerIntent.Execution,
            PlannerWakeReason.TaskFailed => PlannerIntent.Maintenance,
            PlannerWakeReason.WorkerResultReturned => PlannerIntent.Writeback,
            PlannerWakeReason.ApprovalResolved => PlannerIntent.Writeback,
            _ => PlannerIntent.Planning,
        };
    }

    public PlannerIntent Classify(TaskNode task)
    {
        return task.TaskType switch
        {
            TaskType.Planning => PlannerIntent.Planning,
            TaskType.Review => PlannerIntent.Writeback,
            TaskType.Meta => PlannerIntent.Maintenance,
            _ => PlannerIntent.Execution,
        };
    }
}
