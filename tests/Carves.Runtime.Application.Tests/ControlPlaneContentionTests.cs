using System.Text.Json.Nodes;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.ControlPlane;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ControlPlaneContentionTests
{
    [Fact]
    public void ControlPlaneLockService_ExposesLeaseMetadataAndCleansUpOnDispose()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ControlPlaneLockService(workspace.RootPath);
        var resource = Path.Combine(workspace.Paths.TaskNodesRoot, "T-LOCK.json").Replace('\\', '/');
        var workspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-LOCK").Replace('\\', '/');
        var writablePaths = new[] { "src/Sample.cs", "tests/SampleTests.cs" };
        var operationClasses = new[] { "inspect", "edit" };
        var toolsOrAdapters = new[] { "cli", "codex_cli" };

        using (var handle = service.Acquire(
                   AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope,
                   TimeSpan.FromSeconds(1),
                   new ControlPlaneLockOptions
                   {
                       Resource = resource,
                       Operation = "sync-state",
                       Mode = "reconcile",
                       TaskId = "T-LOCK",
                       WorkspacePath = workspacePath,
                       AllowedWritablePaths = writablePaths,
                       AllowedOperationClasses = operationClasses,
                       AllowedToolsOrAdapters = toolsOrAdapters,
                       CleanupPosture = ControlPlaneResidueContract.DryRunRecommendedCleanupPosture,
                   }))
        {
            Assert.NotNull(service.InspectLease(AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope));
            var lease = service.InspectLease(AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope)!;
            Assert.False(string.IsNullOrWhiteSpace(lease.LeaseId));
            Assert.Equal("active", lease.State);
            Assert.Equal("active", lease.Status);
            Assert.Equal(resource, lease.Resource);
            Assert.Equal("sync-state", lease.Operation);
            Assert.Equal("reconcile", lease.Mode);
            Assert.Equal(Environment.ProcessId, lease.OwnerProcessId);
            Assert.Equal("T-LOCK", lease.TaskId);
            Assert.Equal(workspacePath, lease.WorkspacePath);
            Assert.Equal(writablePaths, lease.AllowedWritablePaths);
            Assert.Equal(operationClasses, lease.AllowedOperationClasses);
            Assert.Equal(toolsOrAdapters, lease.AllowedToolsOrAdapters);
            Assert.Equal(ControlPlaneResidueContract.DryRunRecommendedCleanupPosture, lease.CleanupPosture);
            Assert.NotNull(lease.ExpiresAt);
            Assert.Equal(lease.LeaseId, handle.LeaseId);
            Assert.Equal(lease.TaskId, handle.TaskId);
            Assert.Equal(lease.WorkspacePath, handle.WorkspacePath);
            Assert.Contains("sync-state", lease.Summary, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Null(service.InspectLease(AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope));
    }

    [Fact]
    public void ControlPlaneLockService_ReclaimsStaleLeaseBeforeAcquire()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ControlPlaneLockService(workspace.RootPath);
        string leasePath;
        using (var handle = service.Acquire(
                   AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope,
                   TimeSpan.FromSeconds(1),
                   new ControlPlaneLockOptions
                   {
                       Resource = workspace.Paths.TaskGraphFile.Replace('\\', '/'),
                       Operation = "task-graph-save",
                   }))
        {
            Assert.NotNull(handle.Lease);
            leasePath = handle.Lease!.LeasePath.Replace('/', Path.DirectorySeparatorChar);
        }

        File.WriteAllText(leasePath, $$"""
        {
          "scope": "{{AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope}}",
          "resource": "{{workspace.Paths.TaskGraphFile.Replace('\\', '/')}}",
          "operation": "sync-state",
          "mode": "write",
          "owner_id": "stale-owner",
          "owner_process_id": 999999,
          "owner_process_name": "dead-process",
          "acquired_at": "2026-03-01T00:00:00+00:00",
          "last_heartbeat": "2026-03-01T00:00:00+00:00",
          "ttl_seconds": 1
        }
        """);

        using var reacquired = service.Acquire(
            AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope,
            TimeSpan.FromSeconds(1),
            new ControlPlaneLockOptions
            {
                Resource = workspace.Paths.TaskGraphFile.Replace('\\', '/'),
                Operation = "task-graph-save",
            });

        Assert.NotNull(service.InspectLease(AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope));
        var lease = service.InspectLease(AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope)!;
        Assert.Equal("active", lease.State);
        Assert.Equal(Environment.ProcessId, lease.OwnerProcessId);
        Assert.NotEqual("stale-owner", lease.OwnerId);
    }

    [Fact]
    public void ControlPlaneLockService_ReportsOccupiedTimeoutWithPollHintWhenContended()
    {
        using var workspace = new TemporaryWorkspace();
        var holder = new ControlPlaneLockService(workspace.RootPath);
        var contender = new ControlPlaneLockService(workspace.RootPath);
        var scope = AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope;

        using var acquired = holder.Acquire(
            scope,
            TimeSpan.FromSeconds(1),
            new ControlPlaneLockOptions
            {
                Resource = workspace.Paths.TaskGraphFile.Replace('\\', '/'),
                Operation = "sync-state",
            });

        TimeoutException? timeout = null;
        Exception? unexpected = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var _ = contender.Acquire(
                    scope,
                    TimeSpan.FromMilliseconds(100),
                    new ControlPlaneLockOptions
                    {
                        Resource = workspace.Paths.TaskGraphFile.Replace('\\', '/'),
                        Operation = "sync-state",
                    });
            }
            catch (TimeoutException exception)
            {
                timeout = exception;
            }
            catch (Exception exception)
            {
                unexpected = exception;
            }
        });

        thread.Start();
        thread.Join();

        Assert.Null(unexpected);
        Assert.NotNull(timeout);

        Assert.Contains("currently occupied", timeout!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("poll", timeout.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TaskGraphRepository_PersistsParallelUpdatesWithoutCorruptingMachineTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var lockService = new ControlPlaneLockService(workspace.RootPath);
        var repository = new JsonTaskGraphRepository(workspace.Paths, lockService);
        repository.Save(new DomainTaskGraph());

        var tasks = Enumerable.Range(1, 8)
            .Select(index => new TaskNode
            {
                TaskId = $"T-CONTENTION-{index:D3}",
                Title = $"Parallel task {index}",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Scope = [$"src/Parallel/{index:D3}.cs"],
                Acceptance = ["persisted"],
            })
            .ToArray();

        await Task.WhenAll(tasks.Select(task => Task.Run(() => repository.Upsert(task))));

        var loaded = repository.Load();
        var graphJson = JsonNode.Parse(File.ReadAllText(workspace.Paths.TaskGraphFile));

        Assert.Equal(tasks.Length, loaded.Tasks.Count);
        Assert.NotNull(graphJson);
        Assert.All(tasks, task => Assert.True(loaded.Tasks.ContainsKey(task.TaskId), $"Missing task {task.TaskId}"));
    }

    [Fact]
    public async Task RuntimeArtifactRepository_PersistsFailureHistoryUnderParallelWrites()
    {
        using var workspace = new TemporaryWorkspace();
        var lockService = new ControlPlaneLockService(workspace.RootPath);
        var repository = new JsonRuntimeArtifactRepository(workspace.Paths, lockService);
        var failures = Enumerable.Range(1, 6)
            .Select(index => new RuntimeFailureRecord
            {
                FailureId = $"failure-{index:D3}",
                SessionId = "session",
                AttachedRepoRoot = workspace.RootPath,
                TaskId = $"T-FAIL-{index:D3}",
                FailureType = RuntimeFailureType.WorkerExecutionFailure,
                Action = RuntimeFailureAction.AbortTask,
                SessionStatus = RuntimeSessionStatus.Failed,
                TickCount = index,
                Reason = $"Failure {index}",
                Source = "test",
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(index),
            })
            .ToArray();

        await Task.WhenAll(failures.Select(failure => Task.Run(() => repository.SaveRuntimeFailureArtifact(failure))));

        var historyFiles = Directory.EnumerateFiles(workspace.Paths.RuntimeFailureArtifactsRoot, "*.json").ToArray();
        var snapshotJson = JsonNode.Parse(File.ReadAllText(workspace.Paths.RuntimeFailureFile));

        Assert.Equal(failures.Length, historyFiles.Length);
        Assert.NotNull(snapshotJson);
        Assert.Contains(snapshotJson!["failure_id"]!.GetValue<string>(), failures.Select(failure => failure.FailureId));
    }

    [Fact]
    public void AuthoritativeTruthWrite_SucceedsWhileSharedReaderIsOpen()
    {
        using var workspace = new TemporaryWorkspace();
        var lockService = new ControlPlaneLockService(workspace.RootPath);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths, lockService);

        truthStore.WriteAuthoritativeThenMirror(truthStore.TaskGraphFile, workspace.Paths.TaskGraphFile, """{"version":1}""");

        using var reader = new System.IO.FileStream(
            truthStore.TaskGraphFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        truthStore.WriteAuthoritativeThenMirror(truthStore.TaskGraphFile, workspace.Paths.TaskGraphFile, """{"version":2}""");

        Assert.Contains("\"version\":2", SharedFileAccess.ReadAllText(truthStore.TaskGraphFile), StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeTruthWrite_RecordsMirrorSyncContentionInsteadOfRawCollision()
    {
        using var workspace = new TemporaryWorkspace();
        var lockService = new ControlPlaneLockService(workspace.RootPath);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths, lockService);

        truthStore.WriteAuthoritativeThenMirror(truthStore.TaskGraphFile, workspace.Paths.TaskGraphFile, """{"version":1}""");

        using var mirrorReader = new FileStream(
            workspace.Paths.TaskGraphFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        truthStore.WriteAuthoritativeThenMirror(truthStore.TaskGraphFile, workspace.Paths.TaskGraphFile, """{"version":2}""");

        var surface = truthStore.BuildSurface();
        var family = Assert.Single(surface.Families, item => item.FamilyId == "task_graph");

        Assert.Contains("\"version\":2", SharedFileAccess.ReadAllText(truthStore.TaskGraphFile), StringComparison.Ordinal);
        Assert.DoesNotContain("\"version\":2", SharedFileAccess.ReadAllText(workspace.Paths.TaskGraphFile), StringComparison.Ordinal);
        Assert.True(family.MirrorDriftDetected);
        Assert.Equal("drifted", family.MirrorState);
        Assert.Equal("contention", family.MirrorSync.Outcome);
        Assert.Contains("contention", family.MirrorSync.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(family.MirrorSync.LastMirrorSyncAttemptAt);
        Assert.NotNull(family.MirrorSync.LastSuccessfulMirrorSyncAt);
    }
}
