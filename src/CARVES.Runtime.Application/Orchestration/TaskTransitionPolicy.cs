using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed class TaskTransitionPolicy
{
    public TaskTransitionDecision Decide(TaskNode task, TaskRunReport report, PlannerReview review)
    {
        if (!report.WorkerExecution.Succeeded && report.WorkerExecution.Status != WorkerExecutionStatus.Skipped)
        {
            var failureClassification = WorkerFailureSemantics.Classify(report.WorkerExecution);
            if (report.WorkerExecution.Status == WorkerExecutionStatus.ApprovalWait)
            {
                return new TaskTransitionDecision(DomainTaskStatus.ApprovalWait, false, review.Reason);
            }

            if (failureClassification.Lane == WorkerFailureLane.Substrate
                && string.Equals(failureClassification.SubstrateCategory, "delegated_worker_hung", StringComparison.Ordinal)
                && report.WorkerExecution.Retryable
                && review.Verdict == PlannerVerdict.Continue)
            {
                return new TaskTransitionDecision(
                    DomainTaskStatus.Pending,
                    true,
                    "Retryable delegated worker timeout scheduled a bounded runtime retry; semantic replan remains blocked.");
            }

            if (failureClassification.Lane == WorkerFailureLane.Substrate)
            {
                return new TaskTransitionDecision(
                    DomainTaskStatus.Pending,
                    false,
                    $"Execution substrate failure '{failureClassification.SubstrateCategory}' requires runtime correction before semantic replan.");
            }

            if (report.WorkerExecution.Retryable && review.Verdict == PlannerVerdict.Continue)
            {
                return new TaskTransitionDecision(
                    DomainTaskStatus.Pending,
                    true,
                    $"Retryable worker failure '{report.WorkerExecution.FailureKind}' scheduled another attempt.");
            }

            return review.Verdict == PlannerVerdict.Blocked
                ? new TaskTransitionDecision(DomainTaskStatus.Blocked, false, review.Reason)
                : new TaskTransitionDecision(DomainTaskStatus.Review, false, review.Reason);
        }

        if (review.Verdict == PlannerVerdict.Blocked)
        {
            return new TaskTransitionDecision(DomainTaskStatus.Blocked, true, "Safety or execution blocked the task.");
        }

        if (review.Verdict == PlannerVerdict.Superseded)
        {
            return new TaskTransitionDecision(DomainTaskStatus.Superseded, false, "Planner review marked the task superseded.");
        }

        if (!report.DryRun && task.RequiresReviewBoundary && report.Validation.Passed && report.SafetyDecision.Allowed)
        {
            return new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary.");
        }

        if (review.Verdict == PlannerVerdict.Complete)
        {
            return new TaskTransitionDecision(DomainTaskStatus.Completed, false, "Planner review marked the task complete.");
        }

        if (review.Verdict is PlannerVerdict.HumanDecisionRequired or PlannerVerdict.PauseForReview or PlannerVerdict.SplitTask)
        {
            return new TaskTransitionDecision(DomainTaskStatus.Review, !report.Validation.Passed || !report.SafetyDecision.Allowed, "The task requires review before more execution.");
        }

        return new TaskTransitionDecision(DomainTaskStatus.Pending, false, "The task can continue with more execution.");
    }
}
