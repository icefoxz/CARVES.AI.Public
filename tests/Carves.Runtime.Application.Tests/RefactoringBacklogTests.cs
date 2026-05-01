using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;

namespace Carves.Runtime.Application.Tests;

public sealed class RefactoringBacklogTests
{
    [Fact]
    public void RefactoringService_DetectsLifecycleAndMaterializesSuggestedTasks()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/CARVES.Runtime.Host/LargeFile.cs", GenerateLargeClass(190));

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler());
        var service = new RefactoringService(
            workspace.RootPath,
            SystemConfig.CreateDefault("TemporaryWorkspace"),
            new StubGitClient(),
            taskGraphService,
            new RefactoringBacklogRepository(workspace.Paths));

        var detected = service.DetectAndStore();
        Assert.Equal(RefactoringBacklogStatus.Open, Assert.Single(detected.Items).Status);
        var materialized = service.MaterializeSuggestedTasks();
        workspace.WriteFile("src/CARVES.Runtime.Host/LargeFile.cs", GenerateLargeClass(40));
        var resolved = service.DetectAndStore();

        Assert.Single(materialized.SuggestedTaskIds);
        Assert.Single(materialized.QueueIds);
        Assert.Single(materialized.QueuePaths);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json")));
        var suggestedTask = Assert.Single(taskGraphService.Load().ListTasks());
        Assert.Equal(DomainTaskStatus.Suggested, suggestedTask.Status);
        Assert.Equal("REFACTORING_BACKLOG", suggestedTask.Source);
        Assert.Contains("refactoring_queue_id", suggestedTask.Metadata.Keys);
        Assert.Contains("proof_target", suggestedTask.Metadata.Keys);
        Assert.Contains("validation_surface", suggestedTask.Metadata.Keys);
        Assert.Equal(RefactoringBacklogStatus.Resolved, Assert.Single(resolved.Items).Status);
    }

    [Fact]
    public void Materialization_IsDeferredWhenHigherPriorityWorkIsActive()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/CARVES.Runtime.Host/LargeFile.cs", GenerateLargeClass(190));

        var activeGraph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-P1",
                Title = "Active feature work",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Scope = ["src/Feature.cs"],
                Acceptance = ["accepted"],
            },
        ]);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(activeGraph), new ApplicationTaskScheduler());
        var service = new RefactoringService(
            workspace.RootPath,
            SystemConfig.CreateDefault("TemporaryWorkspace"),
            new StubGitClient(),
            taskGraphService,
            new RefactoringBacklogRepository(workspace.Paths));

        service.DetectAndStore();
        var result = service.MaterializeSuggestedTasks();

        Assert.True(result.DeferredForHigherPriorityWork);
        Assert.Empty(result.SuggestedTaskIds);
        Assert.Single(result.QueueIds);
        Assert.Single(result.DeferredBacklogItemIds);
        Assert.Single(taskGraphService.Load().ListTasks());
    }

    [Fact]
    public void MaterializeSuggestedTasks_RotatesCompletedQueueIntoSecondPass()
    {
        using var workspace = new TemporaryWorkspace();
        const string firstPassTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92";

        var activeGraph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = firstPassTaskId,
                Title = "Host bootstrap, dispatch, and composition hotspot queue",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"],
            },
        ]);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(activeGraph), new ApplicationTaskScheduler());
        var backlogRepository = new RefactoringBacklogRepository(workspace.Paths);
        backlogRepository.Save(new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Host/Program.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Host/Program.cs",
                    Reason = "File contains 1300 lines",
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested,
                    SuggestedTaskId = firstPassTaskId,
                },
            ],
        });

        var service = new RefactoringService(
            workspace.RootPath,
            SystemConfig.CreateDefault("TemporaryWorkspace"),
            new StubGitClient(),
            taskGraphService,
            backlogRepository);

        var result = service.MaterializeSuggestedTasks();
        var secondPassTaskId = Assert.Single(result.SuggestedTaskIds);
        var updatedBacklog = backlogRepository.Load();
        var updatedItem = Assert.Single(updatedBacklog.Items);
        var queueIndexPath = Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json");
        var queueSnapshotJson = File.ReadAllText(queueIndexPath);

        Assert.Equal("T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002", secondPassTaskId);
        Assert.Equal(secondPassTaskId, updatedItem.SuggestedTaskId);
        Assert.Equal(RefactoringBacklogStatus.Suggested, updatedItem.Status);
        Assert.Contains(taskGraphService.Load().ListTasks(), task => task.TaskId == secondPassTaskId && task.Status == DomainTaskStatus.Suggested);
        Assert.Contains("\"queue_pass\": 2", queueSnapshotJson, StringComparison.Ordinal);
        Assert.Contains("\"previous_suggested_task_id\": \"T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92\"", queueSnapshotJson, StringComparison.Ordinal);
    }

    private static string GenerateLargeClass(int lineCount)
    {
        var lines = new List<string>
        {
            "namespace Sample.Module;",
            string.Empty,
            "public sealed class LargeFile",
            "{",
        };

        for (var index = 0; index < lineCount; index++)
        {
            lines.Add($"    public int Value{index} {{ get; }} = {index};");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }
}
