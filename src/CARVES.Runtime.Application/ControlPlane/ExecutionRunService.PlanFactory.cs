using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    private static ExecutionStepTemplate CreateTemplate(
        string title,
        ExecutionStepKind kind,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new ExecutionStepTemplate
        {
            Title = title,
            Kind = kind,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static ExecutionRunTriggerReason DetermineTriggerReason(TaskNode task)
    {
        if (task.Metadata.TryGetValue("boundary_replan_strategy", out _))
        {
            return ExecutionRunTriggerReason.Replan;
        }

        return task.RetryCount > 0 || task.Metadata.TryGetValue("last_failure_id", out _)
            ? ExecutionRunTriggerReason.Retry
            : ExecutionRunTriggerReason.Initial;
    }

    private static string BuildImplementStepTitle(ExecutionBoundaryReplanStrategy strategy, ExecutionBoundaryReplanConstraints constraints)
    {
        return strategy switch
        {
            ExecutionBoundaryReplanStrategy.SplitTask => $"Implement the split execution slice within {constraints.MaxFiles} files and {constraints.MaxLinesChanged} lines.",
            ExecutionBoundaryReplanStrategy.RetryWithReducedBudget => $"Retry with reduced budget (<= {constraints.MaxFiles} files, <= {constraints.MaxLinesChanged} lines).",
            _ => $"Implement the narrowed-scope change within {constraints.MaxFiles} files and {constraints.MaxLinesChanged} lines.",
        };
    }

    private static IReadOnlyList<ExecutionStep> FinalizeSteps(
        IReadOnlyList<ExecutionStep> steps,
        FinalizationMode mode,
        string? notes = null)
    {
        var now = DateTimeOffset.UtcNow;
        var writebackIndex = FindStepIndex(steps, ExecutionStepKind.Writeback, steps.Count - 1);
        var finalized = new List<ExecutionStep>(steps.Count);
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var status = mode switch
            {
                FinalizationMode.Stopped when index < writebackIndex => ExecutionStepStatus.Completed,
                FinalizationMode.Stopped when step.Kind == ExecutionStepKind.Writeback => ExecutionStepStatus.Blocked,
                FinalizationMode.Stopped when index > writebackIndex => ExecutionStepStatus.Skipped,
                FinalizationMode.Completed => ExecutionStepStatus.Completed,
                FinalizationMode.Failed when step.Kind == ExecutionStepKind.Cleanup => ExecutionStepStatus.Completed,
                FinalizationMode.Failed => ExecutionStepStatus.Completed,
                _ => step.Status,
            };

            if (mode == FinalizationMode.Stopped && step.Kind == ExecutionStepKind.Cleanup)
            {
                status = ExecutionStepStatus.Skipped;
            }

            finalized.Add(step with
            {
                Status = status,
                StartedAtUtc = step.StartedAtUtc ?? now,
                EndedAtUtc = status is ExecutionStepStatus.Pending or ExecutionStepStatus.InProgress ? null : (step.EndedAtUtc ?? now),
                Notes = notes ?? step.Notes,
            });
        }

        return finalized;
    }

    private static int ResolveStartStepIndex(IReadOnlyList<ExecutionStep> steps)
    {
        var implementIndex = FindStepIndex(steps, ExecutionStepKind.Implement, fallbackIndex: 0);
        return implementIndex;
    }

    private static int FindStepIndex(IReadOnlyList<ExecutionStep> steps, ExecutionStepKind kind, int fallbackIndex)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].Kind == kind)
            {
                return index;
            }
        }

        return Math.Max(0, fallbackIndex);
    }

    private static string ResolveStepTitle(ExecutionRun run)
    {
        if (run.Steps.Count == 0)
        {
            return "(none)";
        }

        var index = Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1);
        return run.Steps[index].Title;
    }

    private enum FinalizationMode
    {
        Completed,
        Failed,
        Stopped,
    }
}
