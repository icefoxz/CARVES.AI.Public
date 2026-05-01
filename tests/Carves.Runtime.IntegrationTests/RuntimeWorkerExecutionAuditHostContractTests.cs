using System.Text.Json;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Infrastructure.Audit;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeWorkerExecutionAuditHostContractTests
{
    [Fact]
    public void RuntimeWorkerExecutionAudit_InspectAndApiProjectSidecarReadModel()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var dbPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "audit.db");
        var readModel = new SqliteWorkerExecutionAuditReadModel(dbPath);
        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-INTEGRATION-WORKER-AUDIT",
            RunId = "worker-run-integration-audit",
            EventType = "completed",
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            ProtocolFamily = "local_cli",
            Status = WorkerExecutionStatus.Succeeded.ToString(),
            FailureKind = WorkerFailureKind.None.ToString(),
            FailureLayer = WorkerFailureLayer.None.ToString(),
            ChangedFilesCount = 1,
            ObservedChangedFilesCount = 1,
            PermissionRequestCount = 0,
            SafetyOutcome = "Allow",
            SafetyAllowed = true,
            Summary = "Integration worker audit entry.",
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        readModel.AppendExecution(new WorkerExecutionAuditEntry
        {
            TaskId = "T-INTEGRATION-WORKER-AUDIT-FAILED",
            RunId = "worker-run-integration-audit-failed",
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
            PermissionRequestCount = 1,
            SafetyOutcome = "Block",
            SafetyAllowed = false,
            Summary = "Integration failed worker audit entry.",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(1),
        });

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-worker-execution-audit");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-worker-execution-audit");
        var filteredApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-worker-execution-audit", "status:Failed,safety:blocked,limit:5");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(0, filteredApi.ExitCode);
        Assert.Contains("Runtime worker execution audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Storage path: .ai/runtime/audit.db", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Non-canonical sidecar: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-INTEGRATION-WORKER-AUDIT", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Supported query fields:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SQLite is a non-canonical operational read model", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-worker-execution-audit", root.GetProperty("surface_id").GetString());
        Assert.Equal(".ai/runtime/audit.db", root.GetProperty("storage_path").GetString());
        Assert.True(root.GetProperty("read_model_configured").GetBoolean());
        Assert.True(root.GetProperty("storage_exists").GetBoolean());
        Assert.True(root.GetProperty("available").GetBoolean());
        Assert.Equal("available", root.GetProperty("availability_status").GetString());
        Assert.Equal(2, root.GetProperty("counts").GetProperty("total_executions").GetInt32());
        Assert.Equal(1, root.GetProperty("counts").GetProperty("succeeded_executions").GetInt32());
        Assert.Equal(2, root.GetProperty("query_counts").GetProperty("total_executions").GetInt32());
        var entry = root.GetProperty("recent_entries")[0];
        Assert.Equal("T-INTEGRATION-WORKER-AUDIT-FAILED", entry.GetProperty("task_id").GetString());

        using var filteredDocument = JsonDocument.Parse(filteredApi.StandardOutput);
        var filteredRoot = filteredDocument.RootElement;
        Assert.Equal("status:Failed,safety:blocked,limit:5", filteredRoot.GetProperty("query").GetProperty("effective_query").GetString());
        Assert.Equal(1, filteredRoot.GetProperty("query_counts").GetProperty("total_executions").GetInt32());
        Assert.Equal(1, filteredRoot.GetProperty("query_counts").GetProperty("failed_executions").GetInt32());
        Assert.Equal(1, filteredRoot.GetProperty("query_counts").GetProperty("safety_blocked_executions").GetInt32());
        var filteredEntry = filteredRoot.GetProperty("recent_entries")[0];
        Assert.Equal("T-INTEGRATION-WORKER-AUDIT-FAILED", filteredEntry.GetProperty("task_id").GetString());
        Assert.Equal("worker-run-integration-audit-failed", filteredEntry.GetProperty("run_id").GetString());
        Assert.Equal("claude_cli", filteredEntry.GetProperty("backend_id").GetString());
    }
}
