using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    public ExecutionRunPlan CreateInitialPlan(TaskNode task)
    {
        return new ExecutionRunPlan
        {
            TaskId = task.TaskId,
            Goal = ResolveGoal(task),
            TriggerReason = DetermineTriggerReason(task),
            Steps =
            [
                CreateTemplate("Inspect task context and authoritative truth.", ExecutionStepKind.Inspect),
                CreateTemplate("Implement the scoped change set.", ExecutionStepKind.Implement),
                CreateTemplate("Verify build, tests, and host-facing evidence.", ExecutionStepKind.Verify),
                CreateTemplate("Write back validated truth to the task graph.", ExecutionStepKind.Writeback),
                CreateTemplate("Clean execution residue and confirm shutdown.", ExecutionStepKind.Cleanup),
            ],
        };
    }

    public ExecutionRunPlan CreateReplanPlan(TaskNode task, ExecutionBoundaryReplanRequest replan, ExecutionRun? previousRun)
    {
        var strategyMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strategy"] = replan.Strategy.ToString(),
            ["violationReason"] = replan.ViolationReason.ToString(),
            ["sourceRunId"] = previousRun?.RunId ?? replan.RunId ?? string.Empty,
        };

        return new ExecutionRunPlan
        {
            TaskId = task.TaskId,
            Goal = $"Replan execution for {task.TaskId}: {ResolveGoal(task)}",
            TriggerReason = ExecutionRunTriggerReason.Replan,
            Metadata = strategyMetadata,
            Steps =
            [
                CreateTemplate("Inspect previous boundary stop and run artifacts.", ExecutionStepKind.Inspect, strategyMetadata),
                CreateTemplate(BuildImplementStepTitle(replan.Strategy, replan.Constraints), ExecutionStepKind.Implement, strategyMetadata),
                CreateTemplate("Verify the reduced-scope execution result.", ExecutionStepKind.Verify, strategyMetadata),
                CreateTemplate("Write back replan outcome after validation passes.", ExecutionStepKind.Writeback, strategyMetadata),
                CreateTemplate("Clean replan residue and preserve run history.", ExecutionStepKind.Cleanup, strategyMetadata),
            ],
        };
    }

    private ExecutionRunPlan CreateFallbackPlan(TaskNode task)
    {
        var plan = CreateInitialPlan(task);
        return plan with { TriggerReason = ExecutionRunTriggerReason.Resume };
    }

    private static string ResolveGoal(TaskNode task)
    {
        return string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
    }
}
