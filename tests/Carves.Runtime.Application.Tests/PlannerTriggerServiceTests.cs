using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerTriggerServiceTests
{
    [Fact]
    public void Apply_RepeatedFailuresRequirePlannerReview()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new PlannerTriggerService(new FailureContextService(repository));
        repository.Append(CreateFailure("FAIL-001", "T-CARD-159-001", FailureType.Unknown, FailureAttributionLayer.Worker, ["src/A.cs"]));
        var latest = CreateFailure("FAIL-002", "T-CARD-159-001", FailureType.Unknown, FailureAttributionLayer.Worker, ["src/B.cs"]);
        repository.Append(latest);

        var updated = service.Apply(CreateTask("T-CARD-159-001"), latest);

        Assert.Equal(DomainTaskStatus.Review, updated.Status);
        Assert.Equal(PlannerVerdict.HumanDecisionRequired, updated.PlannerReview.Verdict);
        Assert.Equal("2", updated.Metadata["failure_count"]);
    }

    [Fact]
    public void Apply_TestRegressionBlocksExecution()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new PlannerTriggerService(new FailureContextService(repository));
        var latest = CreateFailure("FAIL-003", "T-CARD-159-002", FailureType.TestRegression, FailureAttributionLayer.Worker, ["src/Tested.cs"]);
        repository.Append(latest);

        var updated = service.Apply(CreateTask("T-CARD-159-002"), latest);

        Assert.Equal(DomainTaskStatus.Blocked, updated.Status);
        Assert.Equal(PlannerVerdict.Blocked, updated.PlannerReview.Verdict);
    }

    [Fact]
    public void Apply_TaskAttributionMarksNeedsRefinement()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new PlannerTriggerService(new FailureContextService(repository));
        var latest = CreateFailure("FAIL-004", "T-CARD-159-003", FailureType.ScopeDrift, FailureAttributionLayer.Task, ["src/Scope.cs"], confidence: 0.8);
        repository.Append(latest);

        var updated = service.Apply(CreateTask("T-CARD-159-003"), latest);

        Assert.Equal(DomainTaskStatus.Review, updated.Status);
        Assert.Equal("true", updated.Metadata["needs_refinement"]);
        Assert.Equal(PlannerVerdict.SplitTask, updated.PlannerReview.Verdict);
    }

    [Fact]
    public void Apply_RepeatedSameFileChangesReducePatchScope()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new PlannerTriggerService(new FailureContextService(repository));
        repository.Append(CreateFailure("FAIL-005", "T-CARD-159-004", FailureType.Unknown, FailureAttributionLayer.Worker, ["src/Loop.cs"]));
        repository.Append(CreateFailure("FAIL-006", "T-CARD-159-004", FailureType.Unknown, FailureAttributionLayer.Worker, ["src/Loop.cs"]));
        var latest = CreateFailure("FAIL-007", "T-CARD-159-004", FailureType.Unknown, FailureAttributionLayer.Worker, ["src/Loop.cs"]);
        repository.Append(latest);

        var updated = service.Apply(CreateTask("T-CARD-159-004"), latest);

        Assert.Equal(DomainTaskStatus.Review, updated.Status);
        Assert.Equal("reduced", updated.Metadata["patch_scope"]);
        Assert.Equal(nameof(FailureType.InfinitePatchLoop), updated.Metadata["derived_failure_pattern"]);
        Assert.Equal(PlannerVerdict.HumanDecisionRequired, updated.PlannerReview.Verdict);
    }

    [Fact]
    public void CreateBoundaryReplan_SizeExceededMapsToSplitTask()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new PlannerTriggerService(new FailureContextService(repository));
        var task = CreateTask("T-CARD-177-001");
        var violation = new ExecutionBoundaryViolation
        {
            TaskId = task.TaskId,
            RunId = "RUN-T-CARD-177-001-001",
            StoppedAtStep = 4,
            TotalSteps = 5,
            Reason = ExecutionBoundaryStopReason.SizeExceeded,
            Detail = "Touched files exceeded the budget.",
            Budget = new ExecutionBudget
            {
                Size = ExecutionBudgetSize.Medium,
                MaxFiles = 8,
                MaxLinesChanged = 350,
                MaxRetries = 3,
                MaxFailureDensity = 0.5,
            },
        };
        var run = new ExecutionRun
        {
            RunId = "RUN-T-CARD-177-001-001",
            TaskId = task.TaskId,
            Status = ExecutionRunStatus.Stopped,
            TriggerReason = ExecutionRunTriggerReason.Initial,
            Goal = task.Description,
            CurrentStepIndex = 3,
            Steps =
            [
                new ExecutionStep { StepId = "1", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "2", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "3", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "4", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = ExecutionStepStatus.Blocked },
                new ExecutionStep { StepId = "5", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = ExecutionStepStatus.Skipped },
            ],
        };

        var replan = service.CreateBoundaryReplan(task, violation, ".ai/runtime/boundary/violations/T-CARD-177-001.json", run);
        var updated = service.ApplyBoundaryStop(task, violation, replan);

        Assert.Equal(ExecutionBoundaryReplanStrategy.SplitTask, replan.Strategy);
        Assert.Equal(4, replan.Constraints.MaxFiles);
        Assert.Equal(run.RunId, replan.RunId);
        Assert.Equal(4, replan.StoppedAtStep);
        Assert.Equal(DomainTaskStatus.Review, updated.Status);
        Assert.Equal(PlannerVerdict.SplitTask, updated.PlannerReview.Verdict);
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-159",
            Title = "Planner trigger test task",
            Description = "Validate failure-driven planner triggers.",
            Status = DomainTaskStatus.Failed,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/"],
            Acceptance = ["planner reacts to failure state"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static FailureReport CreateFailure(
        string id,
        string taskId,
        FailureType type,
        FailureAttributionLayer layer,
        IReadOnlyList<string> filesInvolved,
        double confidence = 0.6)
    {
        return new FailureReport
        {
            Id = id,
            Timestamp = DateTimeOffset.UtcNow,
            CardId = "CARD-159",
            TaskId = taskId,
            Repo = "CARVES.Runtime",
            Objective = "Planner trigger validation",
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = filesInvolved,
                EstimatedScope = "small",
            },
            Result = new FailureResultSummary
            {
                Status = "failed",
                StopReason = type.ToString(),
            },
            Failure = new FailureDetails
            {
                Type = type,
                Message = type.ToString(),
            },
            Attribution = new FailureAttribution
            {
                Layer = layer,
                Confidence = confidence,
            },
        };
    }
}
