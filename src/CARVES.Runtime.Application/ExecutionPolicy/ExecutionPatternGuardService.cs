using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionPatternGuardService
{
    public TaskNode Apply(TaskNode task, ExecutionPattern pattern)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["execution_pattern_type"] = pattern.Type.ToString(),
            ["execution_pattern_severity"] = pattern.Severity.ToString().ToLowerInvariant(),
            ["execution_pattern_suggestion"] = pattern.Suggestion.ToString(),
            ["execution_pattern_summary"] = pattern.Summary,
            ["execution_pattern_runs_analyzed"] = pattern.RunsAnalyzed.ToString(),
            ["execution_pattern_fingerprint"] = pattern.Fingerprint,
        };

        if (pattern.Type == ExecutionPatternType.HealthyProgress)
        {
            metadata["execution_pattern_warning"] = "false";
            metadata.Remove("planner_signal_execution_pattern");
        }
        else
        {
            metadata["execution_pattern_warning"] = "true";
            metadata["planner_signal_execution_pattern"] = pattern.Type.ToString();
        }

        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
