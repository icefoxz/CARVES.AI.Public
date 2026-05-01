using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeInvariantTests
{
    [Fact]
    public void Stage2RuntimeDocs_Exist()
    {
        var repoRoot = ResolveRepoRoot();
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "runtime-kernel.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "runtime-invariants.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "stage-2-acceptance.md")));
    }

    [Fact]
    public void ReviewWaitSession_RoundTripsWithoutDroppingReviewContext()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeSessionRepository(workspace.Paths);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        session.BeginTick(dryRun: false);
        session.MarkReviewWait("T-REVIEW", "Waiting on review.");

        repository.Save(session);
        var loaded = repository.Load();

        Assert.NotNull(loaded);
        Assert.Equal(RuntimeSessionStatus.ReviewWait, loaded!.Status);
        Assert.False(loaded.DryRun);
        Assert.Equal("T-REVIEW", loaded.LastTaskId);
        Assert.Equal("T-REVIEW", loaded.LastReviewTaskId);
        Assert.Equal("Waiting on review.", loaded.LastReason);
    }

    [Fact]
    public void SuccessfulNonDryRunExecution_StopsAtReviewBoundary()
    {
        var task = new TaskNode
        {
            TaskId = "T-REVIEW-BOUNDARY",
            Title = "Review boundary task",
            Status = DomainTaskStatus.Running,
            Scope = ["src/Feature.cs"],
            Acceptance = ["review boundary holds"],
        };
        var report = CreateReport(task, dryRun: false, validationPassed: true, SafetyDecision.Allow(task.TaskId));
        var review = new PlannerReviewService().Review(task, report);

        var transition = new TaskTransitionPolicy().Decide(task, report, review);

        Assert.Equal(PlannerVerdict.PauseForReview, review.Verdict);
        Assert.Equal(DomainTaskStatus.Review, transition.NextStatus);
        Assert.False(transition.IncrementRetry);
    }

    private static TaskRunReport CreateReport(TaskNode task, bool dryRun, bool validationPassed, SafetyDecision safetyDecision)
    {
        var session = new ExecutionSession(task.TaskId, task.Title, "repo", dryRun, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow);
        return new TaskRunReport
        {
            TaskId = task.TaskId,
            Request = new WorkerRequest
            {
                Task = task,
                Session = session,
            },
            Session = session,
            DryRun = dryRun,
            Validation = new ValidationResult
            {
                Passed = validationPassed,
            },
            Patch = new PatchSummary(1, 0, 0, true, ["src/Feature.cs"]),
            SafetyDecision = safetyDecision,
        };
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
