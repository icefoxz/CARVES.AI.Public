using System.Globalization;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionConfidenceService
{
    public ExecutionConfidence Assess(TaskNode task)
    {
        var totalRuns = ParseInt(task.Metadata, "execution_total_runs");
        var successCount = ParseInt(task.Metadata, "execution_success_count");
        var failureStreak = ParseInt(task.Metadata, "execution_failure_streak");
        var successRate = totalRuns == 0 ? 0.5 : (double)successCount / totalRuns;
        var hasRecentFailures = failureStreak > 0;
        var level = DetermineLevel(totalRuns, successRate, failureStreak);
        var rationale = level switch
        {
            ExecutionConfidenceLevel.High => "High confidence comes from strong historical success with no recent failure streak.",
            ExecutionConfidenceLevel.Low => "Low confidence comes from repeated failures or insufficient stable execution history.",
            _ => "Medium confidence is the neutral baseline when execution history is mixed or sparse.",
        };

        return new ExecutionConfidence
        {
            TotalRuns = totalRuns,
            SuccessCount = successCount,
            FailureStreak = failureStreak,
            SuccessRate = successRate,
            HasRecentFailures = hasRecentFailures,
            Level = level,
            Rationale = rationale,
        };
    }

    private static ExecutionConfidenceLevel DetermineLevel(int totalRuns, double successRate, int failureStreak)
    {
        if (totalRuns >= 3 && successRate >= 0.8 && failureStreak == 0)
        {
            return ExecutionConfidenceLevel.High;
        }

        if (failureStreak >= 2 || (totalRuns >= 2 && successRate < 0.4))
        {
            return ExecutionConfidenceLevel.Low;
        }

        return ExecutionConfidenceLevel.Medium;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var raw)
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
