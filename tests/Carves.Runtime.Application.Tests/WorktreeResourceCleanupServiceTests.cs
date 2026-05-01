using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class WorktreeResourceCleanupServiceTests
{
    [Fact]
    public void Cleanup_RemovesOnlyUntrackedPlanningDraftResidue()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);

        var cardDraftResidue = Path.Combine(workspace.Paths.PlanningCardDraftsRoot, "CARD-351.json");
        var taskgraphDraftResidue = Path.Combine(workspace.Paths.PlanningTaskGraphDraftsRoot, "TG-CARD-351-001.json");
        var trackedHistory = Path.Combine(workspace.Paths.PlanningTaskGraphDraftsRoot, "compact-history", "TG-000.json");

        Directory.CreateDirectory(Path.GetDirectoryName(cardDraftResidue)!);
        Directory.CreateDirectory(Path.GetDirectoryName(taskgraphDraftResidue)!);
        Directory.CreateDirectory(Path.GetDirectoryName(trackedHistory)!);
        File.WriteAllText(cardDraftResidue, """{"card_id":"CARD-351"}""");
        File.WriteAllText(taskgraphDraftResidue, """{"draft_id":"TG-CARD-351-001"}""");
        File.WriteAllText(trackedHistory, """{"draft_id":"TG-000"}""");

        var service = new WorktreeResourceCleanupService(
            workspace.RootPath,
            workspace.Paths,
            config,
            new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()),
            new InMemoryRuntimeSessionRepository(),
            new InMemoryWorkerLeaseRepository(),
            new InMemoryWorktreeRuntimeRepository(),
            new StubUntrackedGitClient([
                ".ai/runtime/planning/card-drafts/CARD-351.json",
                ".ai/runtime/planning/taskgraph-drafts/TG-CARD-351-001.json",
            ]));

        var report = service.Cleanup("test", includeRuntimeResidue: false, includeEphemeralResidue: true);

        Assert.False(File.Exists(cardDraftResidue));
        Assert.False(File.Exists(taskgraphDraftResidue));
        Assert.True(File.Exists(trackedHistory));
        Assert.Equal(2, report.RemovedEphemeralResidueCount);
        Assert.Contains(report.Actions, action => action.Contains("CARD-351.json", StringComparison.Ordinal));
        Assert.Contains(report.Actions, action => action.Contains("TG-CARD-351-001.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Cleanup_RemovesPlatformRuntimeStateTempResidue()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var runtimeInstancesPath = workspace.Paths.PlatformRuntimeInstancesLiveStateFile;
        var tempPath = $"{runtimeInstancesPath}.{Guid.NewGuid():N}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(runtimeInstancesPath)!);
        File.WriteAllText(runtimeInstancesPath, """{"instances":[]}""");
        File.WriteAllText(tempPath, """{"instances":[{"id":"temp"}]}""");

        var service = new WorktreeResourceCleanupService(
            workspace.RootPath,
            workspace.Paths,
            config,
            new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()),
            new InMemoryRuntimeSessionRepository(),
            new InMemoryWorkerLeaseRepository(),
            new InMemoryWorktreeRuntimeRepository(),
            new StubUntrackedGitClient([]));

        var report = service.Cleanup("test", includeRuntimeResidue: false, includeEphemeralResidue: true);

        Assert.False(File.Exists(tempPath));
        Assert.True(File.Exists(runtimeInstancesPath));
        Assert.True(report.RemovedEphemeralResidueCount >= 1);
        Assert.Contains(report.Actions, action => action.Contains(".carves-platform", StringComparison.OrdinalIgnoreCase) && action.Contains(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class InMemoryWorkerLeaseRepository : IWorkerLeaseRepository
    {
        public IReadOnlyList<WorkerLeaseRecord> Load()
        {
            return Array.Empty<WorkerLeaseRecord>();
        }

        public void Save(IReadOnlyList<WorkerLeaseRecord> leases)
        {
        }
    }

    private sealed class StubUntrackedGitClient(IReadOnlyList<string> untrackedPaths) : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return true;
        }

        public override IReadOnlyList<string> GetUntrackedPaths(string repoRoot)
        {
            return untrackedPaths;
        }
    }
}
