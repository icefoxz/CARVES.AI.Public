using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult StatusRuns()
    {
        var runs = executionRunService.ListActiveRuns();
        var lines = new List<string> { "Execution runs:" };
        if (runs.Count == 0)
        {
            lines.Add("- (none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var run in runs)
        {
            lines.Add($"- {run.RunId} [{run.Status}] task={run.TaskId} step {Math.Min(run.Steps.Count, run.CurrentStepIndex + 1)}/{run.Steps.Count}: {ResolveCurrentStep(run)}");
        }

        return new OperatorCommandResult(0, lines);
    }

    private static string ResolveCurrentStep(ExecutionRun run)
    {
        if (run.Steps.Count == 0)
        {
            return "(none)";
        }

        var index = Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1);
        return run.Steps[index].Title;
    }
}
