using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Infrastructure.Audit;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerExecutionAuditReadModelTests
{
    [Fact]
    public void SqliteWorkerExecutionAuditReadModel_AppendsAndQueriesRecentEntries()
    {
        using var workspace = new TemporaryWorkspace();
        var dbPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "audit.db");
        var readModel = new SqliteWorkerExecutionAuditReadModel(dbPath);
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-AUDIT-001",
            RunId = "worker-run-audit-001",
            EventType = "completed",
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            ProtocolFamily = "local_cli",
            Status = WorkerExecutionStatus.Succeeded.ToString(),
            FailureKind = WorkerFailureKind.None.ToString(),
            FailureLayer = WorkerFailureLayer.None.ToString(),
            ChangedFilesCount = 2,
            ObservedChangedFilesCount = 2,
            PermissionRequestCount = 1,
            InputTokens = 100,
            OutputTokens = 20,
            ProviderLatencyMs = 1500,
            SafetyOutcome = "Allow",
            SafetyAllowed = true,
            Summary = "Worker execution completed.",
            OccurredAtUtc = occurredAt,
        });
        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-AUDIT-002",
            RunId = "worker-run-audit-002",
            EventType = "failed",
            BackendId = "claude_cli",
            ProviderId = "claude",
            AdapterId = "ClaudeCliWorkerAdapter",
            ProtocolFamily = "local_cli",
            Status = WorkerExecutionStatus.Failed.ToString(),
            FailureKind = WorkerFailureKind.TaskLogicFailed.ToString(),
            FailureLayer = WorkerFailureLayer.WorkerSemantic.ToString(),
            ChangedFilesCount = 0,
            ObservedChangedFilesCount = 0,
            PermissionRequestCount = 0,
            SafetyOutcome = "Block",
            SafetyAllowed = false,
            Summary = "Worker execution failed.",
            OccurredAtUtc = occurredAt.AddMinutes(1),
        });

        Assert.True(File.Exists(dbPath));
        var recent = readModel.QueryRecent(10);
        var summary = readModel.GetSummary();
        var failed = readModel.Query(new WorkerExecutionAuditQuery
        {
            RequestedQuery = "status:Failed,safety:blocked",
            EffectiveQuery = "status:Failed,safety:blocked,limit:10",
            Status = "Failed",
            SafetyAllowed = false,
            Limit = 10,
        });

        Assert.Equal(2, recent.Count);
        var entry = recent.First();
        Assert.Equal("T-AUDIT-002", entry.TaskId);
        entry = recent.Last();
        Assert.Equal("T-AUDIT-001", entry.TaskId);
        Assert.Equal("worker-run-audit-001", entry.RunId);
        Assert.Equal("codex_cli", entry.BackendId);
        Assert.Equal(2, entry.ChangedFilesCount);
        Assert.Equal(1, entry.PermissionRequestCount);
        Assert.True(entry.SequenceId > 0);
        Assert.Equal(2, summary.TotalExecutions);
        Assert.Equal(1, summary.SucceededExecutions);
        Assert.Equal(1, summary.FailedExecutions);
        Assert.Equal(1, summary.SafetyBlockedExecutions);
        Assert.Equal(1, summary.PermissionRequestCount);
        Assert.Equal(2, summary.ChangedFilesCount);
        Assert.Equal("T-AUDIT-002", summary.LatestTaskId);
        Assert.NotNull(summary.LatestOccurrenceUtc);

        var failedEntry = Assert.Single(failed.Entries);
        Assert.Equal("T-AUDIT-002", failedEntry.TaskId);
        Assert.Equal(1, failed.Summary.TotalExecutions);
        Assert.Equal(1, failed.Summary.FailedExecutions);
        Assert.Equal(1, failed.Summary.SafetyBlockedExecutions);
        Assert.Equal("indexed_sqlite", failed.QueryMode);
    }

    [Fact]
    public void RuntimeWorkerExecutionAuditService_ReportsMissingReadModelAsNotInitialized()
    {
        using var workspace = new TemporaryWorkspace();
        var dbPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "audit.db");
        var readModel = new SqliteWorkerExecutionAuditReadModel(dbPath);

        var surface = new RuntimeWorkerExecutionAuditService(workspace.Paths, readModel).Build();

        Assert.True(surface.ReadModelConfigured);
        Assert.False(surface.StorageExists);
        Assert.True(surface.Available);
        Assert.Equal("not_initialized", surface.AvailabilityStatus);
        Assert.Equal(".ai/runtime/audit.db", surface.StoragePath);
        Assert.Empty(surface.RecentEntries);
        Assert.Equal(0, surface.Counts.TotalExecutions);
        Assert.Contains(surface.Notes, note => note.Contains("non-canonical", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeWorkerExecutionAuditService_AppliesBoundedQueryFilters()
    {
        using var workspace = new TemporaryWorkspace();
        var dbPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "audit.db");
        var readModel = new SqliteWorkerExecutionAuditReadModel(dbPath);
        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-AUDIT-FILTER-001",
            RunId = "worker-run-filter-001",
            EventType = "completed",
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            Status = WorkerExecutionStatus.Succeeded.ToString(),
            FailureKind = WorkerFailureKind.None.ToString(),
            FailureLayer = WorkerFailureLayer.None.ToString(),
            SafetyOutcome = "Allow",
            SafetyAllowed = true,
            Summary = "Filtered audit success.",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
        });
        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-AUDIT-FILTER-002",
            RunId = "worker-run-filter-002",
            EventType = "failed",
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            Status = WorkerExecutionStatus.Failed.ToString(),
            FailureKind = WorkerFailureKind.TaskLogicFailed.ToString(),
            FailureLayer = WorkerFailureLayer.WorkerSemantic.ToString(),
            SafetyOutcome = "Block",
            SafetyAllowed = false,
            Summary = "Filtered audit failure.",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        });

        var surface = new RuntimeWorkerExecutionAuditService(workspace.Paths, readModel)
            .Build("status:Failed,safety:blocked,limit:25,unknown:value");

        Assert.True(surface.Available);
        Assert.Equal(2, surface.Counts.TotalExecutions);
        Assert.Equal(1, surface.QueryCounts.TotalExecutions);
        Assert.Equal(1, surface.QueryCounts.FailedExecutions);
        Assert.Equal(1, surface.QueryCounts.SafetyBlockedExecutions);
        Assert.Equal("status:Failed,safety:blocked,limit:25", surface.Query.EffectiveQuery);
        Assert.Equal("indexed_sqlite", surface.Query.QueryMode);
        Assert.Contains(surface.Query.UnsupportedTerms, term => term == "unknown:value");
        var entry = Assert.Single(surface.RecentEntries);
        Assert.Equal("T-AUDIT-FILTER-002", entry.TaskId);
        Assert.Contains(surface.SupportedQueryFields, field => field == "status:<Succeeded|Failed|Blocked|Skipped|ApprovalWait>");
    }
}
