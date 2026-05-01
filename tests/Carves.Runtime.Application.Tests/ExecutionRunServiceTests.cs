using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionRunServiceTests
{
    [Fact]
    public void PrepareRunForDispatch_CreatesRunningRunUnderTaskScopedRoot()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunService(workspace.Paths);
        var task = CreateTask("T-CARD-179-001");

        var run = service.PrepareRunForDispatch(task);

        Assert.Equal($"RUN-{task.TaskId}-001", run.RunId);
        Assert.Equal(ExecutionRunStatus.Running, run.Status);
        Assert.Equal(5, run.Steps.Count);
        Assert.Equal(ExecutionStepStatus.Completed, run.Steps[0].Status);
        Assert.Equal(ExecutionStepStatus.InProgress, run.Steps[1].Status);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "runs", task.TaskId, $"{run.RunId}.json")));
    }

    [Fact]
    public void PrepareRunForDispatch_AttachesSelectedPackReferenceWhenCurrentSelectionExists()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            PackId = "carves.runtime.core",
            PackVersion = "1.2.3",
            Channel = "stable",
            ArtifactRef = ".ai/artifacts/packs/core-1.2.3.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                RoutingProfile = "connected-lanes",
                EnvironmentProfile = "workspace",
            },
            SelectionMode = "manual_local_assignment",
            Summary = "Selected current local pack.",
        });

        var service = new ExecutionRunService(workspace.Paths, artifactRepository);
        var task = CreateTask("T-CARD-326-001");

        var run = service.PrepareRunForDispatch(task);

        Assert.NotNull(run.SelectedPack);
        Assert.Equal("carves.runtime.core", run.SelectedPack!.PackId);
        Assert.Equal("1.2.3", run.SelectedPack.PackVersion);
        Assert.Equal("stable", run.SelectedPack.Channel);
        Assert.Equal("connected-lanes", run.SelectedPack.RoutingProfile);
    }

    [Fact]
    public void CreateReplanRun_CreatesNewPlannedRunWithoutMutatingStoppedRun()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunService(workspace.Paths);
        var task = CreateTask("T-CARD-182-001");
        var run = service.PrepareRunForDispatch(task);
        var violation = new ExecutionBoundaryViolation
        {
            TaskId = task.TaskId,
            RunId = run.RunId,
            StoppedAtStep = 4,
            TotalSteps = 5,
            Reason = ExecutionBoundaryStopReason.SizeExceeded,
            Detail = "too large",
            Budget = new ExecutionBudget
            {
                MaxFiles = 8,
                MaxLinesChanged = 300,
                MaxRetries = 3,
                MaxFailureDensity = 0.5,
            },
        };
        var replan = new ExecutionBoundaryReplanRequest
        {
            TaskId = task.TaskId,
            RunId = run.RunId,
            StoppedAtStep = 4,
            TotalSteps = 5,
            RunGoal = task.Description,
            Strategy = ExecutionBoundaryReplanStrategy.SplitTask,
            ViolationReason = ExecutionBoundaryStopReason.SizeExceeded,
            ViolationPath = ".ai/runtime/boundary/violations/T-CARD-182-001.json",
            Constraints = new ExecutionBoundaryReplanConstraints
            {
                MaxFiles = 4,
                MaxLinesChanged = 150,
                AllowedChangeKinds = [ExecutionChangeKind.SourceCode],
            },
        };

        var stopped = service.StopRun(run, violation, replan, null, violationPath: "v.json", replanPath: "r.json");
        var nextRun = service.CreateReplanRun(task, replan, stopped);

        Assert.Equal(ExecutionRunStatus.Stopped, stopped.Status);
        Assert.Equal($"RUN-{task.TaskId}-002", nextRun.RunId);
        Assert.Equal(ExecutionRunStatus.Planned, nextRun.Status);
        Assert.Equal(ExecutionRunTriggerReason.Replan, nextRun.TriggerReason);
        Assert.Contains("Split execution slice", nextRun.Steps[1].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReconcileInactiveTaskRun_AbandonsPlannedFollowUpRunAndClearsActiveMetadata()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunService(workspace.Paths);
        var task = CreateTask("T-CARD-182-REVIEW");
        var run = service.PrepareRunForDispatch(task);
        var violation = new ExecutionBoundaryViolation
        {
            TaskId = task.TaskId,
            RunId = run.RunId,
            StoppedAtStep = 4,
            TotalSteps = 5,
            Reason = ExecutionBoundaryStopReason.UnstableExecution,
            Detail = "risk exceeded threshold",
            Budget = new ExecutionBudget
            {
                MaxFiles = 3,
                MaxLinesChanged = 180,
                MaxRetries = 2,
                MaxFailureDensity = 0.35,
            },
        };
        var replan = new ExecutionBoundaryReplanRequest
        {
            TaskId = task.TaskId,
            RunId = run.RunId,
            StoppedAtStep = 4,
            TotalSteps = 5,
            RunGoal = task.Description,
            Strategy = ExecutionBoundaryReplanStrategy.RetryWithReducedBudget,
            ViolationReason = ExecutionBoundaryStopReason.UnstableExecution,
            ViolationPath = ".ai/runtime/boundary/violations/T-CARD-182-REVIEW.json",
            Constraints = new ExecutionBoundaryReplanConstraints
            {
                MaxFiles = 2,
                MaxLinesChanged = 90,
                AllowedChangeKinds = [ExecutionChangeKind.SourceCode],
            },
        };

        var stopped = service.StopRun(run, violation, replan, null, violationPath: "v.json", replanPath: "r.json");
        var plannedFollowUp = service.CreateReplanRun(task, replan, stopped);
        var completedTask = service.ApplyTaskMetadata(task, plannedFollowUp, plannedFollowUp.RunId);
        completedTask.SetStatus(DomainTaskStatus.Completed);

        var reconciled = service.TryReconcileInactiveTaskRun(completedTask, "Review approved the task.");

        Assert.NotNull(reconciled);
        Assert.Equal("Abandoned", reconciled!.Metadata["execution_run_latest_status"]);
        Assert.Equal(plannedFollowUp.RunId, reconciled.Metadata["execution_run_latest_id"]);
        Assert.False(reconciled.Metadata.ContainsKey("execution_run_active_id"));

        var updatedRun = service.Get(plannedFollowUp.RunId);
        Assert.Equal(ExecutionRunStatus.Abandoned, updatedRun.Status);
        Assert.All(updatedRun.Steps, step => Assert.Equal(ExecutionStepStatus.Skipped, step.Status));
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-179",
            Title = "Execution run test",
            Description = "Exercise execution run session truth.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
            Acceptance = ["execution runs persist"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
