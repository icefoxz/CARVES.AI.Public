using Carves.Runtime.Application.Failures;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class FailureReportServiceTests
{
    [Fact]
    public void RuntimeFailure_EmitsAppendOnlyStructuredFailureReport()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var repository = new JsonFailureReportRepository(workspace.Paths);
        var service = new FailureReportService(
            workspace.RootPath,
            repository,
            new FailureClassificationService(),
            artifactRepository);
        var task = new TaskNode
        {
            TaskId = "T-FAIL-001",
            CardId = "CARD-156",
            Title = "Implement failure schema",
            Description = "Implement failure schema foundation.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Failed,
            Scope = ["src/CARVES.Runtime.Application/Failures/", "tests/"],
            Acceptance = ["failure report emitted"],
            RetryCount = 1,
        };
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-12);
        var completedAt = DateTimeOffset.UtcNow;
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = task.TaskId,
            Result = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                RunId = "worker-run-156",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                ProfileId = "extended_dev_ops",
                TrustedProfile = true,
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.Timeout,
                FailureReason = "dotnet test timed out",
                Summary = "dotnet test timed out",
                ChangedFiles = ["src/CARVES.Runtime.Application/Failures/FailureReportService.cs"],
                CommandTrace =
                [
                    new CommandExecutionRecord(
                        ["dotnet", "test"],
                        1,
                        string.Empty,
                        "timed out",
                        false,
                        workspace.RootPath,
                        "validation",
                        completedAt),
                ],
                StartedAt = startedAt,
                CompletedAt = completedAt,
            },
        });
        var failure = new RuntimeFailureRecord
        {
            FailureId = "runtime-failure-156",
            SessionId = "session-156",
            AttachedRepoRoot = workspace.RootPath,
            TaskId = task.TaskId,
            FailureType = RuntimeFailureType.WorkerExecutionFailure,
            Action = RuntimeFailureAction.AbortTask,
            SessionStatus = RuntimeSessionStatus.Failed,
            TickCount = 2,
            Reason = "dotnet test timed out",
            Source = "worker:codex_cli",
            ExceptionType = nameof(WorkerFailureKind.Timeout),
        };

        var report = service.EmitRuntimeFailure(task, failure, RuntimeSessionState.Start(workspace.RootPath, dryRun: true));
        var files = Directory.GetFiles(workspace.Paths.FailuresRoot, "FAIL-*.json", SearchOption.TopDirectoryOnly);

        Assert.Single(files);
        Assert.Equal(report.Id, Path.GetFileNameWithoutExtension(files[0]));
        Assert.Equal(FailureType.Timeout, report.Failure.Type);
        Assert.Equal(FailureAttributionLayer.Provider, report.Attribution.Layer);
        Assert.Equal("failed", report.Result.Status);
        Assert.Equal("codex", report.Provider);
        Assert.Equal("extended_dev_ops", report.ModelProfile);
    }

    [Fact]
    public void BlockedTask_EmitsTaskAttributedFailureReport()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new FailureReportService(
            workspace.RootPath,
            new JsonFailureReportRepository(workspace.Paths),
            new FailureClassificationService(),
            new JsonRuntimeArtifactRepository(workspace.Paths));
        var task = new TaskNode
        {
            TaskId = "T-BLOCKED-001",
            CardId = "CARD-157",
            Title = "Blocked task",
            Description = "Blocked by review.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Blocked,
            Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
            RetryCount = 0,
        };

        var report = service.EmitTaskBlocked(task, "Review blocked the task.");

        Assert.Equal("blocked", report.Result.Status);
        Assert.Equal(FailureAttributionLayer.Task, report.Attribution.Layer);
        Assert.Equal(FailureType.Unknown, report.Failure.Type);
        Assert.True(Directory.Exists(workspace.Paths.FailuresRoot));
    }

    [Fact]
    public void FailureClassification_UsesStableEnums()
    {
        var service = new FailureClassificationService();
        var timeout = service.Classify(
            new RuntimeFailureRecord
            {
                FailureType = RuntimeFailureType.WorkerExecutionFailure,
                Reason = "timed out",
            },
            new WorkerExecutionResult
            {
                FailureKind = WorkerFailureKind.Timeout,
            });
        var reviewRejected = service.Classify(
            new RuntimeFailureRecord
            {
                FailureType = RuntimeFailureType.ReviewRejected,
                Reason = "review rejected",
            });

        Assert.Equal(FailureType.Timeout, timeout);
        Assert.Equal(FailureType.ReviewRejected, reviewRejected);
        Assert.Contains(nameof(FailureType.TestRegression), Enum.GetNames<FailureType>());
        Assert.Contains(nameof(FailureAttributionLayer.Provider), Enum.GetNames<FailureAttributionLayer>());
    }

    [Fact]
    public void FailureClassification_MapsSemanticBuildFailureToWorkerAttribution()
    {
        var service = new FailureClassificationService();
        var workerResult = new WorkerExecutionResult
        {
            FailureKind = WorkerFailureKind.BuildFailure,
            FailureLayer = WorkerFailureLayer.WorkerSemantic,
            FailureReason = "Build failed after changing ResultEnvelope.",
        };
        var failure = new RuntimeFailureRecord
        {
            FailureType = RuntimeFailureType.WorkerExecutionFailure,
            Reason = "Build failed after changing ResultEnvelope.",
        };

        var type = service.Classify(failure, workerResult);
        var attribution = service.BuildAttribution(failure, workerResult);

        Assert.Equal(FailureType.BuildFailure, type);
        Assert.Equal(FailureAttributionLayer.Worker, attribution.Layer);
    }
}
