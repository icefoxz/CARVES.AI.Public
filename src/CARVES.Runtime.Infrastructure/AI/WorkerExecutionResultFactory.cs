using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class WorkerExecutionAdapterContext
{
    public WorkerExecutionAdapterContext(
        string backendId,
        string providerId,
        string adapterId,
        string adapterReason,
        string? protocolFamily,
        string? requestFamily)
    {
        BackendId = backendId;
        ProviderId = providerId;
        AdapterId = adapterId;
        AdapterReason = adapterReason;
        ProtocolFamily = protocolFamily;
        RequestFamily = requestFamily;
    }

    public string BackendId { get; }

    public string ProviderId { get; }

    public string AdapterId { get; }

    public string AdapterReason { get; }

    public string? ProtocolFamily { get; }

    public string? RequestFamily { get; }
}

internal sealed class WorkerExecutionResultDetails
{
    public string? RunId { get; init; }

    public string? ProtocolFamily { get; init; }

    public string? RequestFamily { get; init; }

    public WorkerExecutionStatus Status { get; init; } = WorkerExecutionStatus.Failed;

    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.Unknown;

    public WorkerFailureLayer FailureLayer { get; init; } = WorkerFailureLayer.None;

    public bool Retryable { get; init; }

    public bool Configured { get; init; }

    public string Model { get; init; } = string.Empty;

    public string? RequestId { get; init; }

    public string? ThreadId { get; init; }

    public string RequestPreview { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? Rationale { get; init; }

    public string? FailureReason { get; init; }

    public string? TimeoutPhase { get; init; }

    public string? TimeoutEvidence { get; init; }

    public string? ResponsePreview { get; init; }

    public string? ResponseHash { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ObservedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<WorkerEvent> Events { get; init; } = Array.Empty<WorkerEvent>();

    public IReadOnlyList<WorkerPermissionRequest> PermissionRequests { get; init; } = Array.Empty<WorkerPermissionRequest>();

    public IReadOnlyList<CommandExecutionRecord> CommandTrace { get; init; } = Array.Empty<CommandExecutionRecord>();

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? ProviderStatusCode { get; init; }

    public long? ProviderLatencyMs { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }
}

internal static class WorkerExecutionResultFactory
{
    public static WorkerExecutionResult Create(
        WorkerExecutionRequest request,
        WorkerExecutionAdapterContext adapter,
        WorkerExecutionResultDetails details)
    {
        var runId = string.IsNullOrWhiteSpace(details.RunId)
            ? $"worker-run-{Guid.NewGuid():N}"
            : details.RunId;
        var startedAt = details.StartedAt == default ? DateTimeOffset.UtcNow : details.StartedAt;
        var completedAt = details.CompletedAt == default ? startedAt : details.CompletedAt;

        return new WorkerExecutionResult
        {
            RunId = runId,
            TaskId = request.TaskId,
            BackendId = adapter.BackendId,
            ProviderId = adapter.ProviderId,
            AdapterId = adapter.AdapterId,
            AdapterReason = adapter.AdapterReason,
            ProtocolFamily = details.ProtocolFamily ?? adapter.ProtocolFamily,
            RequestFamily = details.RequestFamily ?? adapter.RequestFamily,
            ProfileId = request.Profile.ProfileId,
            TrustedProfile = request.Profile.Trusted,
            Status = details.Status,
            FailureKind = details.FailureKind,
            FailureLayer = details.FailureLayer,
            Retryable = details.Retryable,
            Configured = details.Configured,
            Model = details.Model,
            RequestId = details.RequestId,
            PriorThreadId = request.PriorThreadId,
            ThreadId = details.ThreadId,
            ThreadContinuity = ResolveThreadContinuity(request.PriorThreadId, details.ThreadId),
            RequestPreview = details.RequestPreview,
            RequestHash = details.RequestHash,
            Summary = details.Summary,
            Rationale = details.Rationale,
            FailureReason = details.FailureReason,
            TimeoutPhase = details.TimeoutPhase,
            TimeoutEvidence = details.TimeoutEvidence,
            ResponsePreview = details.ResponsePreview,
            ResponseHash = details.ResponseHash,
            ChangedFiles = details.ChangedFiles,
            ObservedChangedFiles = details.ObservedChangedFiles,
            Events = details.Events,
            PermissionRequests = details.PermissionRequests,
            CommandTrace = details.CommandTrace,
            InputTokens = details.InputTokens,
            OutputTokens = details.OutputTokens,
            ProviderStatusCode = details.ProviderStatusCode,
            ProviderLatencyMs = details.ProviderLatencyMs,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
    }

    private static WorkerThreadContinuity ResolveThreadContinuity(string? priorThreadId, string? threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return WorkerThreadContinuity.None;
        }

        return !string.IsNullOrWhiteSpace(priorThreadId)
               && string.Equals(priorThreadId, threadId, StringComparison.Ordinal)
            ? WorkerThreadContinuity.ResumedThread
            : WorkerThreadContinuity.NewThread;
    }
}
