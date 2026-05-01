using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerReviewServiceTests
{
    [Fact]
    public void Review_AllowsAutomaticRetryOnlyOnceByDefault()
    {
        var service = new PlannerReviewService();
        var firstAttempt = BuildTask(retryCount: 0);
        var secondAttempt = BuildTask(retryCount: 1);
        var report = BuildRetryableFailureReport();

        var firstReview = service.Review(firstAttempt, report);
        var secondReview = service.Review(secondAttempt, report);

        Assert.Equal(PlannerVerdict.Continue, firstReview.Verdict);
        Assert.Equal(PlannerVerdict.HumanDecisionRequired, secondReview.Verdict);
    }

    [Fact]
    public void Review_BlocksExecutionSubstrateFailureInsteadOfSemanticReplan()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 0);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Blocked,
                FailureKind = WorkerFailureKind.EnvironmentBlocked,
                FailureLayer = WorkerFailureLayer.Environment,
                FailureReason = "Delegated worker preflight failed: DOTNET_CLI_HOME is not writable.",
                Summary = "Delegated worker preflight failed: DOTNET_CLI_HOME is not writable.",
            },
        };

        var review = service.Review(task, report);

        Assert.Equal(PlannerVerdict.Blocked, review.Verdict);
        Assert.Contains("runtime repair", review.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(review.FollowUpSuggestions, item => item.Contains("Repair delegated worker environment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_ApprovalWaitStillTransitionsToApprovalWait()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 0);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.ApprovalWait,
                FailureKind = WorkerFailureKind.ApprovalRequired,
                FailureLayer = WorkerFailureLayer.Environment,
                FailureReason = "Worker execution is awaiting permission approval.",
                Retryable = false,
                Summary = "Worker execution is awaiting permission approval.",
            },
        };

        var review = service.Review(task, report);
        var transition = new TaskTransitionPolicy().Decide(task, report, review);

        Assert.Equal(PlannerVerdict.HumanDecisionRequired, review.Verdict);
        Assert.Equal(Carves.Runtime.Domain.Tasks.TaskStatus.ApprovalWait, transition.NextStatus);
        Assert.False(transition.IncrementRetry);
    }

    [Fact]
    public void Review_UsesBoundedRuntimeRetryForRetryableDelegatedWorkerHung()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 0);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.Timeout,
                FailureLayer = WorkerFailureLayer.Transport,
                FailureReason = "Codex CLI timed out after 180 second(s) after execution activity began.",
                Retryable = true,
                Summary = "Codex CLI timed out after 180 second(s) after execution activity began.",
            },
        };

        var review = service.Review(task, report);
        var transition = new TaskTransitionPolicy().Decide(task, report, review);

        Assert.Equal(PlannerVerdict.Continue, review.Verdict);
        Assert.Contains("bounded runtime retry", review.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(review.FollowUpSuggestions, item => item.Contains("Do not accept partial worktree output", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Carves.Runtime.Domain.Tasks.TaskStatus.Pending, transition.NextStatus);
        Assert.True(transition.IncrementRetry);
    }

    [Fact]
    public void Review_BlocksRetryableDelegatedWorkerHungAfterDefaultRetry()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 1);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.Timeout,
                FailureLayer = WorkerFailureLayer.Transport,
                FailureReason = "Codex CLI timed out after 180 second(s) after execution activity began.",
                Retryable = true,
                Summary = "Codex CLI timed out after 180 second(s) after execution activity began.",
            },
        };

        var review = service.Review(task, report);

        Assert.Equal(PlannerVerdict.Blocked, review.Verdict);
        Assert.Contains("runtime repair", review.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_SemanticFailureRoutesToPlannerReview()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 1);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.BuildFailure,
                FailureLayer = WorkerFailureLayer.WorkerSemantic,
                FailureReason = "Build failed after updating ResultEnvelope.",
                Summary = "Build failed after updating ResultEnvelope.",
            },
        };

        var review = service.Review(task, report);

        Assert.Equal(PlannerVerdict.HumanDecisionRequired, review.Verdict);
        Assert.Contains("Semantic worker failure", review.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(review.FollowUpSuggestions, item => item.Contains("semantic failure evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_ProtocolInvalidOutputBlocksForRuntimeRepair()
    {
        var service = new PlannerReviewService();
        var task = BuildTask(retryCount: 0);
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "gemini_api",
                ProviderId = "gemini",
                AdapterId = "GeminiWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.InvalidOutput,
                FailureLayer = WorkerFailureLayer.Protocol,
                FailureReason = "Remote API worker returned narrative output but did not materialize changed files or submit_result evidence for a patch-capable execution run.",
                Summary = "Remote API worker returned narrative output but did not materialize changed files or submit_result evidence for a patch-capable execution run.",
            },
        };

        var review = service.Review(task, report);

        Assert.Equal(PlannerVerdict.Blocked, review.Verdict);
        Assert.Contains("runtime repair", review.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(review.FollowUpSuggestions, item => item.Contains("Repair execution substrate", StringComparison.OrdinalIgnoreCase));
    }

    private static TaskNode BuildTask(int retryCount)
    {
        return new TaskNode
        {
            TaskId = "T-RETRY-POLICY",
            Title = "Retry policy task",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            RetryCount = retryCount,
            Scope = ["src/RetryPolicy.cs"],
            Acceptance = ["retry is bounded"],
        };
    }

    private static TaskRunReport BuildRetryableFailureReport()
    {
        return new TaskRunReport
        {
            TaskId = "T-RETRY-POLICY",
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = "T-RETRY-POLICY",
                BackendId = "openai_api",
                ProviderId = "openai",
                AdapterId = "RemoteApiWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.Timeout,
                FailureReason = "Transient timeout.",
                Retryable = true,
                Summary = "Transient timeout.",
            },
        };
    }
}
