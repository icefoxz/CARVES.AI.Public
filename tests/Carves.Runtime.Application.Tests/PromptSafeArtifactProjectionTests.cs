using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class PromptSafeArtifactProjectionTests
{
    [Fact]
    public void PromptSafeProjectionFactory_ProjectsCompactWorkerSummaryWithDetailRef()
    {
        var detail = string.Join(
            Environment.NewLine,
            "dotnet build src/CARVES.Runtime.Application/CARVES.Runtime.Application.csproj",
            new string('x', 1200),
            "Build FAILED.",
            "0 Warning(s)",
            "1 Error(s)");

        var result = new WorkerExecutionResult
        {
            TaskId = "T-PROMPT-001",
            BackendId = "openai_api",
            ProviderId = "openai",
            AdapterId = "remote_api",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.BuildFailure,
            Summary = detail,
            FailureReason = detail,
        };

        var projection = PromptSafeArtifactProjectionFactory.ForWorkerExecution("T-PROMPT-001", result);

        Assert.Equal(".ai/artifacts/worker-executions/T-PROMPT-001.json", projection.DetailRef);
        Assert.True(projection.Truncated);
        Assert.True(projection.OriginalLength > projection.Summary.Length);
        Assert.Contains("Build FAILED.", projection.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 400), projection.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorOsEventStreamService_NormalizesLongSummariesAndDeduplicatesDetailArtifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonOperatorOsEventRepository(workspace.Paths);
        var service = new OperatorOsEventStreamService(repository, workspace.Paths);
        var repeatedDetail = string.Join(
            Environment.NewLine,
            "dotnet build src/CARVES.Runtime.Application/CARVES.Runtime.Application.csproj",
            new string('y', 1400),
            "Build FAILED.",
            "0 Warning(s)",
            "1 Error(s)");

        service.Append(new OperatorOsEventRecord
        {
            EventId = "operator-os-event-001",
            EventKind = OperatorOsEventKind.TaskFailed,
            RepoId = "CARVES.Runtime",
            TaskId = "T-PROMPT-001",
            RunId = "RUN-PROMPT-001",
            ReasonCode = "task_failed",
            Summary = repeatedDetail,
        });
        service.Append(new OperatorOsEventRecord
        {
            EventId = "operator-os-event-002",
            EventKind = OperatorOsEventKind.IncidentDetected,
            RepoId = "CARVES.Runtime",
            TaskId = "T-PROMPT-001",
            RunId = "RUN-PROMPT-001",
            ReasonCode = "incident_detected",
            Summary = repeatedDetail,
        });

        var entries = service.Load(taskId: "T-PROMPT-001");
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.True(entry.SummaryTruncated);
            Assert.False(string.IsNullOrWhiteSpace(entry.DetailRef));
            Assert.True(entry.OriginalSummaryLength > entry.Summary.Length);
            Assert.Contains("Build FAILED.", entry.Summary, StringComparison.Ordinal);
        });
        Assert.Equal(entries[0].DetailRef, entries[1].DetailRef);

        var detailPath = Path.Combine(workspace.RootPath, entries[0].DetailRef!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(detailPath));
        using var document = JsonDocument.Parse(File.ReadAllText(detailPath));
        Assert.Equal(repeatedDetail, document.RootElement.GetProperty("detail_text").GetString());
    }
}
