using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectExecutionPattern(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var reportService = new ExecutionRunReportService(paths);
        var reports = reportService.ListReports(taskId);
        var pattern = new ExecutionPatternService().Analyze(taskId, reports);

        var lines = new List<string>
        {
            $"Execution pattern for {taskId}:",
            $"Pattern: {pattern.Type}",
            $"Severity: {pattern.Severity}",
            pattern.RunsAnalyzed == 0
                ? "Fingerprint: (none)"
                : $"Fingerprint: {pattern.Fingerprint}",
            $"Runs analyzed: {pattern.RunsAnalyzed}",
            string.Empty,
            "Evidence:",
        };

        if (pattern.Evidence.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var evidence in pattern.Evidence)
            {
                lines.Add($"- {evidence.RunId} -> {DescribeEvidence(evidence)}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Conclusion:");
        lines.Add($"- {pattern.Summary}");
        lines.Add(string.Empty);
        lines.Add("Suggestion:");
        lines.Add($"- {DescribeSuggestion(pattern.Suggestion)}");
        return new OperatorCommandResult(0, lines);
    }

    private static string DescribeEvidence(ExecutionPatternEvidence evidence)
    {
        var fragments = new List<string> { evidence.RunStatus.ToString() };
        if (evidence.BoundaryReason is not null)
        {
            fragments.Add(evidence.BoundaryReason.Value.ToString());
        }

        if (evidence.ReplanStrategy is not null)
        {
            fragments.Add(evidence.ReplanStrategy.Value.ToString());
        }

        if (evidence.FailureType is not null)
        {
            fragments.Add(evidence.FailureType.ToString()!);
        }

        fragments.Add($"{evidence.CompletedSteps}/{evidence.TotalSteps} steps");
        fragments.Add($"{evidence.FilesChanged} files");
        if (evidence.ModulesTouched.Count > 0)
        {
            fragments.Add($"modules={string.Join(", ", evidence.ModulesTouched)}");
        }

        return string.Join(" | ", fragments);
    }

    private static string DescribeSuggestion(ExecutionPatternSuggestion suggestion)
    {
        return suggestion switch
        {
            ExecutionPatternSuggestion.ManualReview => "Request manual review before further execution.",
            ExecutionPatternSuggestion.NarrowScope => "Narrow scope or reduce the change set before the next run.",
            ExecutionPatternSuggestion.ChangeReplanStrategy => "Change the replan strategy instead of repeating the current loop.",
            ExecutionPatternSuggestion.PauseAndReview => "Pause execution and review why repeated runs are not producing completion.",
            _ => "Continue within the current execution budget.",
        };
    }
}
