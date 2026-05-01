using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerEmergenceServiceTests
{
    [Fact]
    public void CaptureResult_Failure_PersistsReplanIncidentSuggestionAndMemory()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-211-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var service = new PlannerEmergenceService(workspace.Paths, taskGraphService, executionRunService);

        var updated = service.CaptureResult(
            task,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                Status = "failed",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Application/Planning/PlannerEmergenceService.cs"],
                    LinesChanged = 42,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build", "dotnet test"],
                    Build = "success",
                    Tests = "failed",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "test_failure",
                },
                Failure = new ResultEnvelopeFailure
                {
                    Type = nameof(FailureType.TestRegression),
                    Message = "Validation regression",
                },
            },
            new FailureReport
            {
                Id = "FAIL-TEST-001",
                TaskId = task.TaskId,
                CardId = task.CardId,
                Repo = "CARVES.Runtime",
                Objective = task.Description,
                Failure = new FailureDetails
                {
                    Type = FailureType.TestRegression,
                    Message = "Validation regression",
                },
                Attribution = new FailureAttribution
                {
                    Layer = FailureAttributionLayer.Worker,
                    Confidence = 0.9,
                },
            });
        var evidence = new RuntimeEvidenceStoreService(workspace.Paths).ListForTask(task.TaskId, RuntimeEvidenceKind.Planning);

        Assert.Equal("true", updated.Metadata["planner_replan_required"]);
        Assert.Equal(nameof(PlannerReplanTrigger.TaskFailed), updated.Metadata["planner_entry_reason"]);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "planning", "replans", task.TaskId, $"{updated.Metadata["planner_replan_entry_id"]}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "planning", "suggested-tasks", task.TaskId, "SUG-T-CARD-211-001-001.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "memory", "execution", task.TaskId, "MEM-T-CARD-211-001-001.json")));
        Assert.NotEmpty(evidence);
        Assert.Equal(RuntimeEvidenceKind.Planning, evidence[0].Kind);
        Assert.Contains(".ai/execution/T-CARD-211-001/result.json", evidence[0].ArtifactPaths);
    }

    [Fact]
    public void ApproveSuggestedTask_InsertsSuggestedTaskIntoGraph()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-214-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var service = new PlannerEmergenceService(workspace.Paths, taskGraphService, executionRunService);

        var updated = service.CaptureReviewRejection(task, "Review rejected because acceptance was incomplete.");
        var suggestionId = updated.Metadata["planner_suggested_task_ids"];

        var insertion = service.ApproveSuggestedTask(suggestionId, "Approve insertion for follow-up planning.");
        var insertedTask = taskGraphService.GetTask(insertion.InsertedTaskId!);

        Assert.True(insertion.Allowed);
        Assert.Equal(DomainTaskStatus.Suggested, insertedTask.Status);
        Assert.Equal(TaskProposalSource.SuggestedTask, insertedTask.ProposalSource);
    }

    [Fact]
    public void BuildProjection_CountsPlannerEmergenceArtifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-218-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var service = new PlannerEmergenceService(workspace.Paths, taskGraphService, executionRunService);

        task.SetStatus(DomainTaskStatus.Review);
        var updated = service.CaptureReviewOutcome(task, PlannerVerdict.PauseForReview, "Acceptance is not met.");
        taskGraphService.ReplaceTask(updated);
        var projection = service.BuildProjection();

        Assert.Equal(1, projection.ReplanRequiredTaskCount);
        Assert.Equal(1, projection.DraftSuggestedTaskCount);
        Assert.Equal(1, projection.ExecutionMemoryRecordCount);
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-211",
            Title = "Planner emergence test",
            Description = "Exercise replan entry and suggested tasks.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/Planning/"],
            Acceptance = ["planner emergence artifacts exist"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
