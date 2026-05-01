using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionPatternService
{
    private const int DefaultWindow = 5;
    private const int LoopThreshold = 3;
    private const int OverExecutionThreshold = 5;

    public ExecutionPattern Analyze(string taskId, IReadOnlyList<ExecutionRunReport> reports, int window = DefaultWindow)
    {
        var ordered = reports
            .OrderBy(static report => report.RecordedAtUtc)
            .ThenBy(static report => report.RunId, StringComparer.Ordinal)
            .TakeLast(Math.Max(1, window))
            .ToArray();

        if (ordered.Length == 0)
        {
            return Build(taskId, ExecutionPatternType.HealthyProgress, ExecutionPatternSeverity.Low, ExecutionPatternSuggestion.ContinueWithinBudget, "No execution run reports are available yet.", Array.Empty<ExecutionRunReport>());
        }

        if (IsBoundaryLoop(ordered))
        {
            return Build(taskId, ExecutionPatternType.BoundaryLoop, ExecutionPatternSeverity.High, ExecutionPatternSuggestion.NarrowScope, "Repeated boundary-triggered replans are stopping execution without progress.", ordered);
        }

        if (IsReplanLoop(ordered))
        {
            return Build(taskId, ExecutionPatternType.ReplanLoop, ExecutionPatternSeverity.High, ExecutionPatternSuggestion.ChangeReplanStrategy, "The same replan strategy keeps repeating without producing a completed run.", ordered);
        }

        if (IsRepeatedFailure(ordered))
        {
            return Build(taskId, ExecutionPatternType.RepeatedFailure, ExecutionPatternSeverity.High, ExecutionPatternSuggestion.ManualReview, "Recent runs keep failing or stopping with the same execution fingerprint.", ordered);
        }

        if (IsScopeDrift(ordered))
        {
            return Build(taskId, ExecutionPatternType.ScopeDrift, ExecutionPatternSeverity.Medium, ExecutionPatternSuggestion.NarrowScope, "Modules touched are expanding across runs without a completed result.", ordered);
        }

        if (IsOverExecution(reports))
        {
            return Build(taskId, ExecutionPatternType.OverExecution, ExecutionPatternSeverity.High, ExecutionPatternSuggestion.PauseAndReview, "Too many runs have accumulated without a completed execution outcome.", ordered);
        }

        return Build(taskId, ExecutionPatternType.HealthyProgress, ExecutionPatternSeverity.Low, ExecutionPatternSuggestion.ContinueWithinBudget, "Recent execution runs show bounded forward progress.", ordered);
    }

    private static bool IsBoundaryLoop(IReadOnlyList<ExecutionRunReport> reports)
    {
        return reports.Count >= LoopThreshold
               && reports.All(static report => report.RunStatus != ExecutionRunStatus.Completed)
               && reports.Select(static report => report.BoundaryReason).Distinct().Count() == 1
               && reports[0].BoundaryReason is not null
               && reports.Select(static report => report.ReplanStrategy).Distinct().Count() == 1
               && reports[0].ReplanStrategy is not null
               && !HasProgressImprovement(reports);
    }

    private static bool IsReplanLoop(IReadOnlyList<ExecutionRunReport> reports)
    {
        return reports.Count >= LoopThreshold
               && reports.All(static report => report.RunStatus != ExecutionRunStatus.Completed)
               && reports.Select(static report => report.ReplanStrategy).Distinct().Count() == 1
               && reports[0].ReplanStrategy is not null
               && !HasProgressImprovement(reports);
    }

    private static bool IsRepeatedFailure(IReadOnlyList<ExecutionRunReport> reports)
    {
        return reports.Count >= LoopThreshold
               && reports.All(static report => report.RunStatus is ExecutionRunStatus.Failed or ExecutionRunStatus.Stopped)
               && reports.Select(static report => report.Fingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
    }

    private static bool IsScopeDrift(IReadOnlyList<ExecutionRunReport> reports)
    {
        if (reports.Count < LoopThreshold || reports.Any(static report => report.RunStatus == ExecutionRunStatus.Completed))
        {
            return false;
        }

        var first = reports[0].ModulesTouched.ToHashSet(StringComparer.Ordinal);
        var last = reports[^1].ModulesTouched.ToHashSet(StringComparer.Ordinal);
        return last.Except(first, StringComparer.Ordinal).Any()
               && reports.Select(static report => string.Join('|', report.ModulesTouched)).Distinct(StringComparer.Ordinal).Count() > 1;
    }

    private static bool IsOverExecution(IReadOnlyList<ExecutionRunReport> reports)
    {
        return reports.Count >= OverExecutionThreshold
               && reports.All(static report => report.RunStatus != ExecutionRunStatus.Completed);
    }

    private static bool HasProgressImprovement(IReadOnlyList<ExecutionRunReport> reports)
    {
        return reports[^1].CompletedSteps > reports[0].CompletedSteps;
    }

    private static ExecutionPattern Build(
        string taskId,
        ExecutionPatternType type,
        ExecutionPatternSeverity severity,
        ExecutionPatternSuggestion suggestion,
        string summary,
        IReadOnlyList<ExecutionRunReport> reports)
    {
        return new ExecutionPattern
        {
            TaskId = taskId,
            Type = type,
            Severity = severity,
            Suggestion = suggestion,
            Summary = summary,
            Fingerprint = reports.LastOrDefault()?.Fingerprint ?? string.Empty,
            RunsAnalyzed = reports.Count,
            Evidence = reports.Select(static report => new ExecutionPatternEvidence
            {
                RunId = report.RunId,
                RunStatus = report.RunStatus,
                BoundaryReason = report.BoundaryReason,
                FailureType = report.FailureType,
                ReplanStrategy = report.ReplanStrategy,
                FilesChanged = report.FilesChanged,
                CompletedSteps = report.CompletedSteps,
                TotalSteps = report.TotalSteps,
                ModulesTouched = report.ModulesTouched,
            }).ToArray(),
        };
    }
}
