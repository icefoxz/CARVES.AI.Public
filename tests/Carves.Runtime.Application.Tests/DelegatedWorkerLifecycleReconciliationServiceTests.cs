using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class DelegatedWorkerLifecycleReconciliationServiceTests
{
    [Fact]
    public void ReconcileKnownDrift_QuarantinesExpiredDelegatedRunAndBlocksTask()
    {
        using var workspace = new TemporaryWorkspace();
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-RECON-001",
            Title = "Reconcile drift",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            LastWorkerRunId = "run-recon-001",
            LastWorkerBackend = "codex_cli",
            Scope = ["README.md"],
            Acceptance = ["runtime truth is stable"],
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, dryRun: false));

        var leaseRepository = new JsonWorkerLeaseRepository(workspace.Paths);
        leaseRepository.Save(
        [
            new WorkerLeaseRecord
            {
                LeaseId = "lease-recon-001",
                NodeId = "local-default",
                RepoPath = workspace.RootPath,
                SessionId = "default",
                TaskId = "T-RECON-001",
                Status = WorkerLeaseStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletionReason = "Lease expired after heartbeat timeout.",
            },
        ]);

        IRuntimeArtifactRepository artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = "T-RECON-001",
            Result = new WorkerExecutionResult
            {
                TaskId = "T-RECON-001",
                RunId = "run-recon-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.TaskLogicFailed,
                Retryable = false,
                Summary = "synthetic expired delegated run",
            },
        });

        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-RECON-001");
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "README.md"), "orphaned worktree");
        var worktreeService = new WorktreeRuntimeService(
            workspace.RootPath,
            new StubGitClient(),
            new JsonWorktreeRuntimeRepository(workspace.Paths));
        worktreeService.RecordPrepared("T-RECON-001", worktreePath, "abc123", "run-recon-001");

        var lifecycleRepository = new JsonDelegatedRunLifecycleRepository(workspace.Paths);
        var service = new DelegatedWorkerLifecycleReconciliationService(
            workspace.Paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            worktreeService,
            artifactRepository,
            lifecycleRepository,
            new JsonDelegatedRunRecoveryLedgerRepository(workspace.Paths));

        var snapshot = service.ReconcileKnownDrift();
        var record = Assert.Single(snapshot.Records, item => string.Equals(item.TaskId, "T-RECON-001", StringComparison.Ordinal));
        var task = taskGraphService.GetTask("T-RECON-001");
        var rebuilt = new JsonWorktreeRuntimeRepository(workspace.Paths).Load();
        var ledger = new JsonDelegatedRunRecoveryLedgerRepository(workspace.Paths).Load();
        var entry = Assert.Single(ledger.Entries, item => string.Equals(item.TaskId, "T-RECON-001", StringComparison.Ordinal));

        Assert.Equal(DelegatedRunLifecycleState.Quarantined, record.State);
        Assert.Equal(DomainTaskStatus.Blocked, task.Status);
        Assert.Equal(WorkerRecoveryAction.EscalateToOperator, task.LastRecoveryAction);
        Assert.Contains("delegated worker", task.LastRecoveryReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(rebuilt.PendingRebuilds, item => string.Equals(item.TaskId, "T-RECON-001", StringComparison.Ordinal));
        Assert.Equal("lease-recon-001", entry.LeaseId);
        Assert.Equal(WorkerRecoveryAction.EscalateToOperator, entry.RecoveryAction);
        Assert.Equal(DomainTaskStatus.Pending, entry.TaskStatusBefore);
        Assert.Equal(DomainTaskStatus.Blocked, entry.TaskStatusAfter);
    }

    [Fact]
    public void ReconcileKnownDrift_DoesNotOverwriteUnrelatedReviewBoundary()
    {
        using var workspace = new TemporaryWorkspace();
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-REVIEW-001",
            Title = "Existing review",
            Status = DomainTaskStatus.Review,
            TaskType = TaskType.Execution,
            Scope = ["README.md"],
            Acceptance = ["review remains authoritative"],
        });
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-RECON-002",
            Title = "Reconcile drift",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            LastWorkerRunId = "run-recon-002",
            LastWorkerBackend = "codex_cli",
            Scope = ["README.md"],
            Acceptance = ["runtime truth is stable"],
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        session.MarkReviewWait("T-REVIEW-001", "Existing review boundary.");
        sessionRepository.Save(session);

        var leaseRepository = new JsonWorkerLeaseRepository(workspace.Paths);
        leaseRepository.Save(
        [
            new WorkerLeaseRecord
            {
                LeaseId = "lease-recon-002",
                NodeId = "local-default",
                RepoPath = workspace.RootPath,
                SessionId = "default",
                TaskId = "T-RECON-002",
                Status = WorkerLeaseStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletionReason = "Lease expired after heartbeat timeout.",
            },
        ]);

        IRuntimeArtifactRepository artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = "T-RECON-002",
            Result = new WorkerExecutionResult
            {
                TaskId = "T-RECON-002",
                RunId = "run-recon-002",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.TaskLogicFailed,
                Retryable = false,
                Summary = "synthetic expired delegated run",
            },
        });

        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-RECON-002");
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "README.md"), "quarantined worktree");
        var worktreeRepository = new JsonWorktreeRuntimeRepository(workspace.Paths);
        var worktreeService = new WorktreeRuntimeService(
            workspace.RootPath,
            new StubGitClient(),
            worktreeRepository);
        worktreeService.RecordPrepared("T-RECON-002", worktreePath, "abc123", "run-recon-002");
        var worktreeSnapshot = worktreeRepository.Load();
        var quarantinedRecord = worktreeSnapshot.Records.Single(record => string.Equals(record.TaskId, "T-RECON-002", StringComparison.Ordinal));
        quarantinedRecord.State = WorktreeRuntimeState.Quarantined;
        quarantinedRecord.QuarantineReason = "Synthetic quarantine should not overwrite unrelated review wait.";
        worktreeRepository.Save(worktreeSnapshot);

        var lifecycleRepository = new JsonDelegatedRunLifecycleRepository(workspace.Paths);
        var service = new DelegatedWorkerLifecycleReconciliationService(
            workspace.Paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            worktreeService,
            artifactRepository,
            lifecycleRepository,
            new JsonDelegatedRunRecoveryLedgerRepository(workspace.Paths));

        service.ReconcileKnownDrift();
        var updatedSession = sessionRepository.Load() ?? throw new InvalidOperationException("Expected runtime session.");

        Assert.Equal(RuntimeSessionStatus.ReviewWait, updatedSession.Status);
        Assert.Equal("Existing review boundary.", updatedSession.WaitingReason);
        Assert.Equal("Existing review boundary.", updatedSession.LastReason);
        Assert.Contains("T-REVIEW-001", updatedSession.ReviewPendingTaskIds);
        Assert.Equal(WorkerRecoveryAction.RebuildWorktree, updatedSession.LastRecoveryAction);
        Assert.Contains("T-RECON-002", updatedSession.LastRecoveryReason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void RehydrateAfterHostRestart_DoesNotReopenFinalizedTaskWithQuarantinedWorktree()
    {
        using var workspace = new TemporaryWorkspace();
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-FINAL-QUARANTINE",
            Title = "Finalized task with quarantine residue",
            Status = DomainTaskStatus.Superseded,
            TaskType = TaskType.Execution,
            LastWorkerRunId = "run-final-quarantine",
            LastWorkerBackend = "codex_cli",
            Scope = ["README.md"],
            Acceptance = ["final task truth is not reopened"],
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, dryRun: false));

        var leaseRepository = new JsonWorkerLeaseRepository(workspace.Paths);
        leaseRepository.Save(
        [
            new WorkerLeaseRecord
            {
                LeaseId = "lease-final-quarantine",
                NodeId = "local-default",
                RepoPath = workspace.RootPath,
                SessionId = "default",
                TaskId = "T-FINAL-QUARANTINE",
                Status = WorkerLeaseStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletionReason = "Lease expired before task was superseded.",
            },
        ]);

        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-FINAL-QUARANTINE");
        Directory.CreateDirectory(worktreePath);
        var worktreeRepository = new JsonWorktreeRuntimeRepository(workspace.Paths);
        var worktreeService = new WorktreeRuntimeService(
            workspace.RootPath,
            new StubGitClient(),
            worktreeRepository);
        worktreeService.RecordPrepared("T-FINAL-QUARANTINE", worktreePath, "abc123", "run-final-quarantine");
        var worktreeSnapshot = worktreeRepository.Load();
        var quarantinedRecord = worktreeSnapshot.Records.Single(record => string.Equals(record.TaskId, "T-FINAL-QUARANTINE", StringComparison.Ordinal));
        quarantinedRecord.State = WorktreeRuntimeState.Quarantined;
        quarantinedRecord.QuarantineReason = "Historical quarantine residue after task supersession.";
        worktreeRepository.Save(worktreeSnapshot);

        var ledgerRepository = new JsonDelegatedRunRecoveryLedgerRepository(workspace.Paths);
        var service = new DelegatedWorkerLifecycleReconciliationService(
            workspace.Paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            worktreeService,
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new JsonDelegatedRunLifecycleRepository(workspace.Paths),
            ledgerRepository);

        var report = service.RehydrateAfterHostRestart("Resident host restart rehydration.");
        var record = Assert.Single(
            service.Capture(persist: false).Records,
            item => string.Equals(item.TaskId, "T-FINAL-QUARANTINE", StringComparison.Ordinal));
        var task = taskGraphService.GetTask("T-FINAL-QUARANTINE");

        Assert.Equal(0, report.ReconciledTaskCount);
        Assert.Equal(DelegatedRunLifecycleState.Completed, record.State);
        Assert.Equal(DomainTaskStatus.Superseded, task.Status);
        Assert.DoesNotContain(
            ledgerRepository.Load().Entries,
            item => string.Equals(item.TaskId, "T-FINAL-QUARANTINE", StringComparison.Ordinal));
    }
}
