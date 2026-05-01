using Carves.Runtime.Application.Failures;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class FailureSummaryServiceTests
{
    [Fact]
    public void Build_AggregatesFailuresDeterministically()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        repository.Append(CreateFailure("FAIL-001", "T-CARD-160-002", "src/B.cs", FailureType.TestRegression, "codex_cli"));
        repository.Append(CreateFailure("FAIL-002", "T-CARD-160-001", "src/A.cs", FailureType.ScopeDrift, "codex_cli"));
        repository.Append(CreateFailure("FAIL-003", "T-CARD-160-001", "src/A.cs", FailureType.ScopeDrift, "claude_cli"));
        var service = new FailureSummaryService(new FailureContextService(repository));

        var summary = service.Build();

        Assert.Equal(3, summary.TotalFailures);
        Assert.Equal("ScopeDrift", summary.TopTypes[0].Key);
        Assert.Equal(2, summary.TopTypes[0].Count);
        Assert.Equal("T-CARD-160-001", summary.TopTasks[0].Key);
        Assert.Equal(2, summary.TopTasks[0].Count);
        Assert.Equal("src/A.cs", summary.TopFiles[0].Key);
        Assert.Equal(2, summary.TopFiles[0].Count);
        Assert.Equal("codex_cli", summary.ProviderFailureRates[0].ProviderId);
        Assert.Equal(2d / 3d, summary.ProviderFailureRates[0].Rate, 3);
    }

    [Fact]
    public void RenderLines_TaskFilterShowsScopedFailures()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonFailureReportRepository(workspace.Paths);
        repository.Append(CreateFailure("FAIL-004", "T-CARD-160-003", "src/C.cs", FailureType.Timeout, "codex_cli"));
        repository.Append(CreateFailure("FAIL-005", "T-CARD-160-004", "src/D.cs", FailureType.Unknown, "codex_cli"));
        var service = new FailureSummaryService(new FailureContextService(repository));

        var lines = service.RenderLines(service.Build("T-CARD-160-003"));

        Assert.Contains(lines, line => line.Contains("Failures for T-CARD-160-003:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Total failures: 1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("FAIL-004", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("FAIL-005", StringComparison.Ordinal));
    }

    private static FailureReport CreateFailure(string id, string taskId, string filePath, FailureType type, string provider)
    {
        return new FailureReport
        {
            Id = id,
            Timestamp = DateTimeOffset.UtcNow,
            CardId = "CARD-160",
            TaskId = taskId,
            Repo = "CARVES.Runtime",
            Provider = provider,
            Objective = "Failure summary validation",
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = [filePath],
                EstimatedScope = "small",
            },
            Result = new FailureResultSummary
            {
                Status = "failed",
                StopReason = type.ToString(),
            },
            Failure = new FailureDetails
            {
                Type = type,
                Message = type.ToString(),
            },
            Attribution = new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.5,
            },
        };
    }
}
