using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class GovernedTruthTransitionProfileService
{
    public const string TaskTruthHostRoute = "host.result_ingestion.task_truth_transition";
    public const string RunToReviewHostRoute = "host.result_ingestion.run_to_review";
    public const string PrivilegedExpectedHostRoutePrefix = "host.privileged_transition";
    public const string PrivilegedExpectedTerminalStatePrefix = "privileged_transition_authorized";

    public IReadOnlyList<StateTransitionOperation> BuildTaskTruthTransitions(
        TaskNode originalTask,
        TaskNode nextTask,
        string operation,
        string? reviewSubmissionId = null)
    {
        var transitions = new List<StateTransitionOperation>();
        if (!string.IsNullOrWhiteSpace(reviewSubmissionId)
            && string.Equals(operation, "task_status_to_review", StringComparison.Ordinal))
        {
            transitions.Add(BuildReviewSubmissionRecordedTransition(reviewSubmissionId));
        }

        transitions.Add(BuildTaskStatusTransition(
            originalTask.TaskId,
            originalTask.Status,
            nextTask.Status,
            operation));
        return transitions;
    }

    public IReadOnlyList<StateTransitionOperation> BuildRunToReviewTransitions(
        string taskId,
        DomainTaskStatus currentStatus,
        string submissionId)
    {
        return
        [
            BuildReviewSubmissionRecordedTransition(submissionId),
            new StateTransitionOperation
            {
                Root = ".ai/tasks/",
                Operation = "task_status_to_review",
                ObjectId = taskId,
                From = currentStatus.ToString(),
                To = DomainTaskStatus.Review.ToString().ToUpperInvariant(),
            },
        ];
    }

    public IReadOnlyList<StateTransitionOperation> BuildPrivilegedExpectedTransitions(
        string operationId,
        string targetKind,
        string targetId)
    {
        return
        [
            new StateTransitionOperation
            {
                Root = ResolvePrivilegedTargetRoot(operationId, targetKind),
                Operation = operationId,
                ObjectId = targetId,
                From = "pending_authorization",
                To = "authorized",
            },
        ];
    }

    public string ResolvePrivilegedExpectedHostRoute(string operationId)
    {
        return $"{PrivilegedExpectedHostRoutePrefix}.{NormalizeOperationSegment(operationId)}";
    }

    public string ResolvePrivilegedExpectedTerminalState(string operationId)
    {
        return $"{PrivilegedExpectedTerminalStatePrefix}:{NormalizeOperationSegment(operationId)}";
    }

    public string ResolveTaskStatusTransitionOperation(DomainTaskStatus status)
    {
        return status switch
        {
            DomainTaskStatus.Review => "task_status_to_review",
            DomainTaskStatus.Completed => "task_status_to_completed",
            DomainTaskStatus.Failed => "task_status_to_failed",
            DomainTaskStatus.Blocked => "task_status_to_blocked",
            _ => $"task_status_to_{status.ToString().ToLowerInvariant()}",
        };
    }

    private static StateTransitionOperation BuildReviewSubmissionRecordedTransition(string submissionId)
    {
        return new StateTransitionOperation
        {
            Root = ".ai/artifacts/worker-executions/",
            Operation = "review_submission_recorded",
            ObjectId = submissionId,
            From = "absent",
            To = "recorded",
        };
    }

    private static StateTransitionOperation BuildTaskStatusTransition(
        string taskId,
        DomainTaskStatus from,
        DomainTaskStatus to,
        string operation)
    {
        return new StateTransitionOperation
        {
            Root = ".ai/tasks/",
            Operation = operation,
            ObjectId = taskId,
            From = from.ToString(),
            To = to.ToString().ToUpperInvariant(),
        };
    }

    private static string ResolvePrivilegedTargetRoot(string operationId, string targetKind)
    {
        if (string.Equals(operationId, "release_channel", StringComparison.Ordinal))
        {
            return ".carves-platform/";
        }

        return targetKind switch
        {
            "memory_proposal" or "memory_truth" => ".ai/memory/",
            "codegraph_projection" or "codegraph" => ".ai/codegraph/",
            "card" or "taskgraph" or "task" => ".ai/tasks/",
            _ => ".ai/runtime/",
        };
    }

    private static string NormalizeOperationSegment(string operationId)
    {
        return string.IsNullOrWhiteSpace(operationId) ? "unknown" : operationId.Trim();
    }
}
