using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Workers;

public sealed record WorkerExecutionAuditEntry
{
    public long? SequenceId { get; init; }

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string? ProtocolFamily { get; init; }

    public string Status { get; init; } = string.Empty;

    public string FailureKind { get; init; } = string.Empty;

    public string FailureLayer { get; init; } = string.Empty;

    public int ChangedFilesCount { get; init; }

    public int ObservedChangedFilesCount { get; init; }

    public int PermissionRequestCount { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public long? ProviderLatencyMs { get; init; }

    public string SafetyOutcome { get; init; } = string.Empty;

    public bool SafetyAllowed { get; init; }

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static WorkerExecutionAuditEntry From(TaskRunReport report)
    {
        var workerExecution = report.WorkerExecution;
        return new WorkerExecutionAuditEntry
        {
            TaskId = report.TaskId,
            RunId = workerExecution.RunId,
            EventType = ResolveEventType(workerExecution),
            BackendId = workerExecution.BackendId,
            ProviderId = workerExecution.ProviderId,
            AdapterId = workerExecution.AdapterId,
            ProtocolFamily = workerExecution.ProtocolFamily,
            Status = workerExecution.Status.ToString(),
            FailureKind = workerExecution.FailureKind.ToString(),
            FailureLayer = workerExecution.FailureLayer.ToString(),
            ChangedFilesCount = workerExecution.ChangedFiles.Count,
            ObservedChangedFilesCount = workerExecution.ObservedChangedFiles.Count,
            PermissionRequestCount = workerExecution.PermissionRequests.Count,
            InputTokens = workerExecution.InputTokens,
            OutputTokens = workerExecution.OutputTokens,
            ProviderLatencyMs = workerExecution.ProviderLatencyMs,
            SafetyOutcome = report.SafetyDecision.Outcome.ToString(),
            SafetyAllowed = report.SafetyDecision.Allowed,
            Summary = workerExecution.Summary,
            OccurredAtUtc = workerExecution.CompletedAt,
        };
    }

    private static string ResolveEventType(WorkerExecutionResult workerExecution)
    {
        if (workerExecution.Status == WorkerExecutionStatus.Skipped)
        {
            return "skipped";
        }

        if (workerExecution.Status == WorkerExecutionStatus.ApprovalWait)
        {
            return "approval_wait";
        }

        return workerExecution.Succeeded ? "completed" : "failed";
    }
}

public sealed record WorkerExecutionAuditSummary
{
    public int TotalExecutions { get; init; }

    public int SucceededExecutions { get; init; }

    public int FailedExecutions { get; init; }

    public int BlockedExecutions { get; init; }

    public int SkippedExecutions { get; init; }

    public int ApprovalWaitExecutions { get; init; }

    public int SafetyBlockedExecutions { get; init; }

    public int PermissionRequestCount { get; init; }

    public int ChangedFilesCount { get; init; }

    public DateTimeOffset? LatestOccurrenceUtc { get; init; }

    public string? LatestTaskId { get; init; }
}
