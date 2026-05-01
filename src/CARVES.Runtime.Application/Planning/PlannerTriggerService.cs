using System.Globalization;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerTriggerService
{
    private const double TaskAttributionThreshold = 0.7;

    private readonly FailureContextService failureContextService;

    public PlannerTriggerService(FailureContextService failureContextService)
    {
        this.failureContextService = failureContextService;
    }

    public TaskNode Apply(TaskNode task, FailureReport? failure)
    {
        var failures = failureContextService.GetTaskFailures(task.TaskId, int.MaxValue);
        var repeatedFiles = failureContextService.GetRepeatedFileFailures(task.TaskId);
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["failure_count"] = failures.Count.ToString(CultureInfo.InvariantCulture),
            ["recent_failure_ids"] = string.Join(',', failures.Take(3).Select(item => item.Id)),
        };

        var status = task.Status;
        var review = task.PlannerReview;

        if (repeatedFiles.Any(item => item.Value >= 3))
        {
            metadata["patch_scope"] = "reduced";
            metadata["derived_failure_pattern"] = nameof(FailureType.InfinitePatchLoop);
            review = new PlannerReview
            {
                Verdict = PlannerVerdict.SplitTask,
                Reason = "Repeated unsuccessful modification on the same files indicates an infinite patch loop.",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = true,
                FollowUpSuggestions = repeatedFiles
                    .Where(item => item.Value >= 3)
                    .Select(item => $"Reduce patch scope around {item.Key}.")
                    .ToArray(),
            };
            status = DomainTaskStatus.Review;
        }

        if (failure is not null)
        {
            metadata["last_failure_id"] = failure.Id;
            metadata["last_failure_type"] = failure.Failure.Type.ToString();

            if (failure.Attribution.Layer == FailureAttributionLayer.Task && failure.Attribution.Confidence >= TaskAttributionThreshold)
            {
                metadata["needs_refinement"] = "true";
                review = new PlannerReview
                {
                    Verdict = PlannerVerdict.SplitTask,
                    Reason = "Failure attribution indicates the task definition needs refinement before more execution.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = true,
                    FollowUpSuggestions = ["Refine task scope or split the task before retrying."],
                };
                status = DomainTaskStatus.Review;
            }

            if (failure.Failure.Type == FailureType.ReviewRejected)
            {
                review = new PlannerReview
                {
                    Verdict = PlannerVerdict.SplitTask,
                    Reason = "Review rejected the result; rollback completed and planner follow-up is required.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions = ["Return to planning before dispatching another execution attempt."],
                };
                status = DomainTaskStatus.Review;
            }

            if (failure.Failure.Type == FailureType.TestRegression)
            {
                review = new PlannerReview
                {
                    Verdict = PlannerVerdict.Blocked,
                    Reason = "Test regression blocks further execution until the failure is resolved.",
                    AcceptanceMet = false,
                    BoundaryPreserved = false,
                    ScopeDriftDetected = true,
                    FollowUpSuggestions = ["Fix the regression before any further execution."],
                };
                status = DomainTaskStatus.Blocked;
            }

            if (failures.Count >= 2 && status != DomainTaskStatus.Blocked)
            {
                review = new PlannerReview
                {
                    Verdict = PlannerVerdict.HumanDecisionRequired,
                    Reason = "Repeated failures on the same task require planner review before continuing.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions = ["Planner review is required because the task failed multiple times."],
                };
                status = DomainTaskStatus.Review;
            }
        }

        return Clone(task, status, review, metadata);
    }

    public ExecutionBoundaryReplanRequest CreateBoundaryReplan(TaskNode task, ExecutionBoundaryViolation violation, string violationPath, ExecutionRun? run = null)
    {
        var strategy = violation.Reason switch
        {
            ExecutionBoundaryStopReason.SizeExceeded => ExecutionBoundaryReplanStrategy.SplitTask,
            ExecutionBoundaryStopReason.Timeout => ExecutionBoundaryReplanStrategy.SplitTask,
            ExecutionBoundaryStopReason.RetryExceeded => ExecutionBoundaryReplanStrategy.NarrowScope,
            ExecutionBoundaryStopReason.ScopeViolation => ExecutionBoundaryReplanStrategy.NarrowScope,
            ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath => ExecutionBoundaryReplanStrategy.NarrowScope,
            ExecutionBoundaryStopReason.ManagedWorkspaceDeniedPath => ExecutionBoundaryReplanStrategy.NarrowScope,
            ExecutionBoundaryStopReason.UnstableExecution => ExecutionBoundaryReplanStrategy.RetryWithReducedBudget,
            _ => ExecutionBoundaryReplanStrategy.NarrowScope,
        };

        return new ExecutionBoundaryReplanRequest
        {
            TaskId = task.TaskId,
            RunId = run?.RunId ?? violation.RunId,
            StoppedAtStep = violation.StoppedAtStep,
            TotalSteps = violation.TotalSteps,
            RunGoal = run?.Goal,
            Strategy = strategy,
            ViolationReason = violation.Reason,
            ViolationPath = violationPath,
            Constraints = new ExecutionBoundaryReplanConstraints
            {
                MaxFiles = Math.Max(1, violation.Budget.MaxFiles / 2),
                MaxLinesChanged = Math.Max(40, violation.Budget.MaxLinesChanged / 2),
                AllowedChangeKinds = violation.Budget.ChangeKinds,
            },
            FollowUpSuggestions = strategy switch
            {
                ExecutionBoundaryReplanStrategy.SplitTask =>
                [
                    "Split the task into smaller execution slices before the next dispatch.",
                    $"Keep each slice at or below {Math.Max(1, violation.Budget.MaxFiles / 2)} files.",
                ],
                ExecutionBoundaryReplanStrategy.RetryWithReducedBudget =>
                [
                    "Retry only after reducing the effective execution budget.",
                    $"Limit the retry to {Math.Max(1, violation.Budget.MaxFiles / 2)} files and {Math.Max(40, violation.Budget.MaxLinesChanged / 2)} changed lines.",
                ],
                ExecutionBoundaryReplanStrategy.NarrowScope when violation.Reason == ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath =>
                [
                    "Remove host-only truth mutations from the workspace result before dispatching again.",
                    "Route governed truth updates back through host-routed review/writeback instead of editing them in the workspace.",
                ],
                ExecutionBoundaryReplanStrategy.NarrowScope when violation.Reason == ExecutionBoundaryStopReason.ManagedWorkspaceDeniedPath =>
                [
                    "Remove denied-root or secret-like mutations from the result before dispatching again.",
                    "Keep execution inside the repo-local workspace and away from VCS internals or machine-level files.",
                ],
                _ =>
                [
                    "Narrow the execution scope before dispatching again.",
                    $"Stay within {Math.Max(1, violation.Budget.MaxFiles / 2)} files and the existing allowed change kinds.",
                ],
            },
        };
    }

    public TaskNode ApplyBoundaryStop(TaskNode task, ExecutionBoundaryViolation violation, ExecutionBoundaryReplanRequest replan)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["planner_required"] = "true",
            ["boundary_replan_strategy"] = replan.Strategy.ToString(),
            ["boundary_violation_reason"] = violation.Reason.ToString(),
        };

        var verdict = replan.Strategy switch
        {
            ExecutionBoundaryReplanStrategy.SplitTask => PlannerVerdict.SplitTask,
            _ => PlannerVerdict.PauseForReview,
        };

        var review = new PlannerReview
        {
            Verdict = verdict,
            Reason = string.IsNullOrWhiteSpace(violation.Detail)
                ? $"Execution stopped by the boundary gate: {violation.Reason}."
                : $"Execution stopped by the boundary gate: {violation.Detail}",
            AcceptanceMet = false,
            BoundaryPreserved = false,
            ScopeDriftDetected = violation.Reason is ExecutionBoundaryStopReason.ScopeViolation
                or ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath
                or ExecutionBoundaryStopReason.ManagedWorkspaceDeniedPath,
            FollowUpSuggestions = replan.FollowUpSuggestions,
        };

        return Clone(task, DomainTaskStatus.Review, review, metadata);
    }

    private static TaskNode Clone(
        TaskNode task,
        DomainTaskStatus status,
        PlannerReview review,
        IReadOnlyDictionary<string, string> metadata)
    {
        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = status,
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
            PlannerReview = review,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
