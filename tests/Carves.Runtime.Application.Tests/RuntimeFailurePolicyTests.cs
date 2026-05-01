using System.Text.Json;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeFailurePolicyTests
{
    [Fact]
    public void FailurePolicy_ClassifiesKnownFailureTypes()
    {
        var policy = new RuntimeFailurePolicy();
        var session = RuntimeSessionState.Start("C:\\repo", dryRun: false);
        session.BeginTick(dryRun: false);

        var schemaFailure = policy.ClassifyException(session, "T-1", new JsonException("Invalid JSON."));
        var controlPlaneFailure = policy.ClassifyException(session, "T-1", new InvalidDataException("Desync."));
        var schedulerFailure = policy.ClassifyException(session, "T-1", new SchedulerDecisionException("Scheduler broke."));
        var workerFailure = policy.ClassifyException(session, "T-1", new InvalidOperationException("Worker crashed."));

        Assert.Equal(RuntimeFailureType.SchemaMismatch, schemaFailure.FailureType);
        Assert.Equal(RuntimeFailureAction.EscalateToOperator, schemaFailure.Action);
        Assert.Equal(RuntimeFailureType.ControlPlaneDesync, controlPlaneFailure.FailureType);
        Assert.Equal(RuntimeFailureAction.PauseSession, controlPlaneFailure.Action);
        Assert.Equal(RuntimeFailureType.SchedulerDecisionFailure, schedulerFailure.FailureType);
        Assert.Equal(RuntimeFailureAction.PauseSession, schedulerFailure.Action);
        Assert.Equal(RuntimeFailureType.WorkerExecutionFailure, workerFailure.FailureType);
        Assert.Equal(RuntimeFailureAction.AbortTask, workerFailure.Action);
    }

    [Fact]
    public void ReviewRejection_IsRecordedAsRetryableFailure()
    {
        var policy = new RuntimeFailurePolicy();
        var session = RuntimeSessionState.Start("C:\\repo", dryRun: false);
        session.MarkReviewWait("T-REVIEW", "Awaiting approval.");

        var failure = policy.CreateReviewRejected(session, "T-REVIEW", "Needs another pass.");

        Assert.Equal(RuntimeFailureType.ReviewRejected, failure.FailureType);
        Assert.Equal(RuntimeFailureAction.RetryTask, failure.Action);
        Assert.Equal("T-REVIEW", failure.TaskId);
        Assert.Equal(RuntimeSessionStatus.ReviewWait, failure.SessionStatus);
    }

    [Fact]
    public void RuntimeFailureArtifacts_AreVersionedAndPersisted()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var policy = new RuntimeFailurePolicy();
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        session.BeginTick(dryRun: false);

        var failure = policy.ClassifyException(session, "T-FAILURE", new InvalidOperationException("Worker exploded."));
        repository.SaveRuntimeFailureArtifact(failure);

        Assert.Contains("\"schema_version\": 1", File.ReadAllText(workspace.Paths.RuntimeFailureFile), StringComparison.Ordinal);
        Assert.True(Directory.EnumerateFiles(workspace.Paths.RuntimeFailureArtifactsRoot, "*.json").Any());
    }

    [Fact]
    public void RuntimeFailureArtifacts_ReadLegacyLatestFailurePathAndPersistToLiveStateRoot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var legacyPath = Path.Combine(workspace.Paths.RuntimeRoot, "last_failure.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """
        {
          "schema_version": 1,
          "failure_id": "failure-legacy",
          "session_id": "default",
          "attached_repo_root": "D:/repo",
          "task_id": "T-LEGACY",
          "failure_type": "worker_execution_failure",
          "action": "abort_task",
          "session_status": "failed",
          "tick_count": 1,
          "reason": "legacy failure",
          "source": "legacy",
          "captured_at": "2026-03-31T00:00:00+00:00"
        }
        """);

        var loaded = repository.TryLoadLatestRuntimeFailure();

        Assert.NotNull(loaded);
        Assert.Equal("failure-legacy", loaded!.FailureId);

        repository.SaveRuntimeFailureArtifact(loaded);

        Assert.True(File.Exists(workspace.Paths.RuntimeFailureFile));
    }
}
