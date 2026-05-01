using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    public ExecutionRun CompleteRun(ExecutionRun run, string? resultEnvelopePath)
    {
        var steps = FinalizeSteps(run.Steps, FinalizationMode.Completed);
        var completed = run with
        {
            Status = ExecutionRunStatus.Completed,
            CurrentStepIndex = Math.Max(0, steps.Count - 1),
            EndedAtUtc = DateTimeOffset.UtcNow,
            ResultEnvelopePath = resultEnvelopePath ?? run.ResultEnvelopePath,
            Steps = steps,
        };
        Save(completed);
        return completed;
    }

    public ExecutionRun FailRun(ExecutionRun run, string? resultEnvelopePath, string? notes = null)
    {
        var steps = FinalizeSteps(run.Steps, FinalizationMode.Failed, notes);
        var failed = run with
        {
            Status = ExecutionRunStatus.Failed,
            CurrentStepIndex = Math.Max(0, steps.Count - 1),
            EndedAtUtc = DateTimeOffset.UtcNow,
            ResultEnvelopePath = resultEnvelopePath ?? run.ResultEnvelopePath,
            Steps = steps,
        };
        Save(failed);
        return failed;
    }

    public ExecutionRun StopRun(
        ExecutionRun run,
        ExecutionBoundaryViolation violation,
        ExecutionBoundaryReplanRequest replan,
        string? resultEnvelopePath,
        string? violationPath,
        string? replanPath)
    {
        var steps = FinalizeSteps(run.Steps, FinalizationMode.Stopped, violation.Detail);
        var stoppedStepIndex = FindStepIndex(steps, ExecutionStepKind.Writeback, fallbackIndex: Math.Max(0, steps.Count - 1));
        var stopped = run with
        {
            Status = ExecutionRunStatus.Stopped,
            CurrentStepIndex = stoppedStepIndex,
            EndedAtUtc = DateTimeOffset.UtcNow,
            ResultEnvelopePath = resultEnvelopePath ?? run.ResultEnvelopePath,
            BoundaryViolationPath = violationPath,
            ReplanArtifactPath = replanPath,
            Metadata = new Dictionary<string, string>(run.Metadata, StringComparer.Ordinal)
            {
                ["boundaryReason"] = violation.Reason.ToString(),
                ["boundaryReplanStrategy"] = replan.Strategy.ToString(),
            },
            Steps = steps,
        };
        Save(stopped);
        return stopped;
    }

    public ExecutionRun AbandonRun(ExecutionRun run, string? notes = null)
    {
        var now = DateTimeOffset.UtcNow;
        var steps = run.Steps.Select(step =>
        {
            var status = step.Status switch
            {
                ExecutionStepStatus.Pending or ExecutionStepStatus.InProgress => ExecutionStepStatus.Skipped,
                _ => step.Status,
            };

            return status == step.Status
                ? step with
                {
                    Notes = notes ?? step.Notes,
                }
                : step with
                {
                    Status = status,
                    EndedAtUtc = step.EndedAtUtc ?? now,
                    Notes = notes ?? step.Notes,
                };
        }).ToArray();
        var abandoned = run with
        {
            Status = ExecutionRunStatus.Abandoned,
            EndedAtUtc = now,
            Steps = steps,
        };
        Save(abandoned);
        return abandoned;
    }

}
