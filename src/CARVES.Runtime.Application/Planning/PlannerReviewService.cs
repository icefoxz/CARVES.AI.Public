using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerReviewService
{
    public PlannerReview Review(TaskNode task, TaskRunReport report)
    {
        if (!report.WorkerExecution.Succeeded && report.WorkerExecution.Status != WorkerExecutionStatus.Skipped)
        {
            var classification = WorkerFailureSemantics.Classify(report.WorkerExecution);
            if (report.WorkerExecution.Status == WorkerExecutionStatus.ApprovalWait)
            {
                return new PlannerReview
                {
                    Verdict = PlannerVerdict.HumanDecisionRequired,
                    Reason = string.IsNullOrWhiteSpace(report.WorkerExecution.FailureReason)
                        ? "Worker execution is awaiting permission approval."
                        : report.WorkerExecution.FailureReason,
                    DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                };
            }

            if (classification.Lane == WorkerFailureLane.Substrate)
            {
                if (CanUseRuntimeRetryForDelegatedWorkerHung(task, report.WorkerExecution, classification))
                {
                    return new PlannerReview
                    {
                        Verdict = PlannerVerdict.Continue,
                        Reason = "Retryable delegated worker timeout will use bounded runtime retry; semantic replan remains blocked until the execution substrate converges.",
                        DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                        AcceptanceMet = false,
                        BoundaryPreserved = true,
                        ScopeDriftDetected = false,
                        FollowUpSuggestions =
                        [
                            $"Review stuck-run evidence before retrying the task ({classification.NextAction}).",
                            "Do not accept partial worktree output or create a semantic replan until a governed worker result passes validation.",
                        ],
                    };
                }

                return new PlannerReview
                {
                    Verdict = PlannerVerdict.Blocked,
                    Reason = $"Delegated execution substrate failure '{classification.SubstrateCategory ?? classification.ReasonCode}' requires runtime repair before semantic replan.",
                    DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions =
                    [
                        $"Repair execution substrate before retrying the task ({classification.NextAction}).",
                        "Do not create a semantic replan entry for the original task until the substrate issue is cleared.",
                    ],
                };
            }

            if (report.WorkerExecution.Retryable && task.RetryCount < 1)
            {
                return new PlannerReview
                {
                    Verdict = PlannerVerdict.Continue,
                    Reason = $"Retryable worker failure '{report.WorkerExecution.FailureKind}' will be retried once by default.",
                    DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                };
            }

            return new PlannerReview
            {
                Verdict = report.WorkerExecution.FailureKind == WorkerFailureKind.PolicyDenied
                    ? PlannerVerdict.Blocked
                    : PlannerVerdict.HumanDecisionRequired,
                Reason = classification.Lane == WorkerFailureLane.Semantic
                    ? $"Semantic worker failure '{classification.ReasonCode}' requires planner review before another attempt."
                    : (string.IsNullOrWhiteSpace(report.WorkerExecution.FailureReason)
                        ? $"Worker failed with '{report.WorkerExecution.FailureKind}'."
                        : report.WorkerExecution.FailureReason),
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = classification.Lane == WorkerFailureLane.Semantic
                    ? ["Review the semantic failure evidence before retrying or creating a replan task."]
                    : report.WorkerExecution.Retryable
                        ? ["Use `task retry <task-id> <reason...>` for an explicit bounded retry."]
                        : Array.Empty<string>(),
            };
        }

        if (report.SafetyDecision.Outcome == SafetyOutcome.Blocked)
        {
            return new PlannerReview
            {
                Verdict = PlannerVerdict.Blocked,
                Reason = "Safety layer rejected the runtime step.",
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = false,
                ScopeDriftDetected = true,
            };
        }

        if (report.SafetyDecision.Outcome == SafetyOutcome.NeedsReview)
        {
            return new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Safety policy requires human review before the task can continue.",
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = report.SafetyDecision.Issues.Select(issue => $"{issue.Code}: {issue.Message}").ToArray(),
            };
        }

        if (!report.Validation.Passed)
        {
            return new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = "Validation failed; another attempt would increase decision debt.",
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            };
        }

        return new PlannerReview
        {
            Verdict = report.DryRun ? PlannerVerdict.Continue : PlannerVerdict.PauseForReview,
            Reason = report.DryRun
                ? "Dry-run completed; real execution is still required."
                : $"Execution passed validation and awaits human review for {task.TaskId}.",
            DecisionStatus = report.DryRun ? ReviewDecisionStatus.NeedsAttention : ReviewDecisionStatus.PendingReview,
            AcceptanceMet = !report.DryRun && report.SafetyDecision.Allowed,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
        };
    }

    private static bool CanUseRuntimeRetryForDelegatedWorkerHung(
        TaskNode task,
        WorkerExecutionResult workerExecution,
        WorkerFailureClassification classification)
    {
        return task.RetryCount < 1
            && workerExecution.Retryable
            && string.Equals(classification.SubstrateCategory, "delegated_worker_hung", StringComparison.Ordinal);
    }
}
