using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
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

public sealed class RuntimeConsistencyCheckServiceTests
{
    [Fact]
    public void Run_FlagsExpiredDelegatedRunThatCollapsedBackToPending()
    {
        using var workspace = new TemporaryWorkspace();
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-DRIFT-001",
            Title = "Synthetic drift",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            LastWorkerRunId = "run-drift-001",
            LastWorkerBackend = "codex_cli",
            LastWorkerFailureKind = WorkerFailureKind.InvalidOutput,
            LastWorkerRetryable = true,
            LastWorkerSummary = "Synthetic worker failure.",
            LastRecoveryAction = WorkerRecoveryAction.Retry,
            LastRecoveryReason = "Retryable worker failure.",
            RetryNotBefore = DateTimeOffset.UtcNow.AddMinutes(5),
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, dryRun: false));

        var leaseRepository = new JsonWorkerLeaseRepository(workspace.Paths);
        leaseRepository.Save(
        [
            new WorkerLeaseRecord
            {
                LeaseId = "lease-drift-001",
                NodeId = "local-default",
                RepoPath = workspace.RootPath,
                SessionId = "default",
                TaskId = "T-DRIFT-001",
                Status = WorkerLeaseStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletionReason = "Lease expired after heartbeat timeout.",
            },
        ]);

        IRuntimeArtifactRepository artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = "T-DRIFT-001",
            Result = new WorkerExecutionResult
            {
                TaskId = "T-DRIFT-001",
                RunId = "run-drift-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                AdapterReason = "Synthetic adapter.",
                ProfileId = "extended_dev_ops",
                TrustedProfile = true,
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.InvalidOutput,
                Retryable = true,
                Configured = true,
                Model = "gpt-5-codex",
                RequestPreview = "preview",
                RequestHash = "hash",
                Summary = "Synthetic worker failure.",
            },
        });

        var service = new RuntimeConsistencyCheckService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            artifactRepository,
            new JsonDelegatedRunLifecycleRepository(workspace.Paths));

        var report = service.Run();

        var finding = Assert.Single(report.Findings, item => string.Equals(item.Category, "expired_run_collapsed_to_pending", StringComparison.Ordinal));
        Assert.Equal(RuntimeConsistencySeverity.Warning, finding.Severity);
        Assert.Equal("T-DRIFT-001", finding.TaskId);
        Assert.Equal("lease-drift-001", finding.LeaseId);
        Assert.Equal(workspace.Paths.PlatformWorkerLeasesLiveStateFile, finding.PlatformTruthAnchor);
    }

    [Fact]
    public void Run_DoesNotFlagRetryableExpiredRunOnceLifecycleTruthIsReconciled()
    {
        using var workspace = new TemporaryWorkspace();
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-DRIFT-002",
            Title = "Retryable drift",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            LastWorkerRunId = "run-drift-002",
            LastWorkerBackend = "codex_cli",
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, dryRun: false));

        var leaseRepository = new JsonWorkerLeaseRepository(workspace.Paths);
        leaseRepository.Save(
        [
            new WorkerLeaseRecord
            {
                LeaseId = "lease-drift-002",
                NodeId = "local-default",
                RepoPath = workspace.RootPath,
                SessionId = "default",
                TaskId = "T-DRIFT-002",
                Status = WorkerLeaseStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletionReason = "Lease expired after heartbeat timeout.",
            },
        ]);

        var lifecycleRepository = new JsonDelegatedRunLifecycleRepository(workspace.Paths);
        lifecycleRepository.Save(new DelegatedRunLifecycleSnapshot
        {
            Records =
            [
                new DelegatedRunLifecycleRecord
                {
                    TaskId = "T-DRIFT-002",
                    LeaseId = "lease-drift-002",
                    LeaseStatus = WorkerLeaseStatus.Expired,
                    RunId = "run-drift-002",
                    BackendId = "codex_cli",
                    TaskStatus = DomainTaskStatus.Pending,
                    State = DelegatedRunLifecycleState.Retryable,
                    RecoveryAction = WorkerRecoveryAction.Retry,
                    Retryable = true,
                    ReasonCode = "delegated_run_retryable",
                    Summary = "Retryable delegated run already reconciled.",
                    RecommendedNextAction = "rerun delegated execution",
                },
            ],
        });

        var service = new RuntimeConsistencyCheckService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            new JsonRuntimeArtifactRepository(workspace.Paths),
            lifecycleRepository);

        var report = service.Run();

        Assert.DoesNotContain(report.Findings, item => string.Equals(item.Category, "expired_run_collapsed_to_pending", StringComparison.Ordinal));
    }
}
