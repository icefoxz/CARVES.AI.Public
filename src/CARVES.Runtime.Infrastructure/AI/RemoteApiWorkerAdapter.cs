using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal abstract class RemoteApiWorkerAdapter : IWorkerAdapter
{
    private readonly IProviderProtocol protocol;
    private readonly IHttpTransport transport;

    protected RemoteApiWorkerAdapter(IProviderProtocol protocol, IHttpTransport transport, string selectionReason)
    {
        this.protocol = protocol;
        this.transport = transport;
        SelectionReason = selectionReason;
    }

    public abstract string AdapterId { get; }

    public abstract string BackendId { get; }

    public string ProviderId => protocol.ProviderId;

    public bool IsConfigured => protocol.IsConfigured;

    public bool IsRealAdapter => true;

    public string SelectionReason { get; }

    public WorkerProviderCapabilities GetCapabilities()
    {
        var metadata = protocol.Metadata;
        return new WorkerProviderCapabilities
        {
            SupportsExecution = true,
            SupportsEventStream = false,
            SupportsHealthProbe = true,
            SupportsCancellation = false,
            SupportsTrustedProfiles = false,
            SupportsNetworkAccess = true,
            SupportsDotNetBuild = true,
            SupportsLongRunningTasks = false,
            SupportsStreaming = metadata.SupportsStreaming,
            SupportsToolCalls = metadata.SupportsToolCalls,
            SupportsJsonMode = metadata.SupportsJsonMode,
            SupportsSystemPrompt = metadata.SupportsSystemPrompt,
            SupportsFileUpload = metadata.SupportsFileUpload,
        };
    }

    public WorkerBackendHealthSummary CheckHealth()
    {
        return protocol.CheckHealth();
    }

    public WorkerRunControlResult Cancel(string runId, string reason)
    {
        return new WorkerRunControlResult
        {
            BackendId = BackendId,
            RunId = runId,
            Supported = false,
            Succeeded = false,
            Summary = $"{AdapterId} does not support cancellation.",
        };
    }

    public WorkerExecutionResult Execute(WorkerExecutionRequest request)
    {
        var requestPreview = BuildPreview(request.Input);
        var requestHash = Hash(request.Input);
        var startedAt = DateTimeOffset.UtcNow;
        var adapterContext = new WorkerExecutionAdapterContext(
            BackendId,
            ProviderId,
            AdapterId,
            SelectionReason,
            protocol.Metadata.ProtocolFamily,
            protocol.Metadata.RequestFamily);

        ProviderProtocolResult protocolResult;
        try
        {
            var transportRequest = protocol.BuildRequest(request);
            var transportResponse = transport.Send(transportRequest);
            protocolResult = protocol.ParseResponse(request, transportResponse);
        }
        catch (Exception exception)
        {
            protocolResult = protocol.FromException(request, exception);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var runId = protocolResult.RequestId ?? $"worker-run-{Guid.NewGuid():N}";
        var responseText = FirstNonEmpty(protocolResult.OutputText, protocolResult.RawResponse, protocolResult.FailureReason, protocolResult.Summary);
        var commandTrace = BuildCommandTrace(request, protocolResult, startedAt, completedAt);
        return WorkerExecutionResultFactory.Create(
            request,
            adapterContext,
            new WorkerExecutionResultDetails
            {
                RunId = runId,
                Status = protocolResult.Status,
                FailureKind = protocolResult.FailureKind,
                FailureLayer = protocolResult.FailureLayer,
                Retryable = protocolResult.Retryable,
                Configured = protocolResult.Configured,
                Model = protocolResult.Model,
                RequestId = protocolResult.RequestId,
                RequestPreview = requestPreview,
                RequestHash = requestHash,
                Summary = FirstNonEmpty(protocolResult.Summary, protocolResult.OutputText, protocolResult.FailureReason, SelectionReason),
                Rationale = protocolResult.OutputText,
                FailureReason = protocolResult.FailureReason,
                ResponsePreview = BuildPreview(responseText),
                ResponseHash = string.IsNullOrWhiteSpace(responseText) ? null : Hash(responseText),
                Events = NormalizeEvents(runId, request.TaskId, protocolResult.Events),
                CommandTrace = commandTrace,
                InputTokens = protocolResult.InputTokens,
                OutputTokens = protocolResult.OutputTokens,
                ProviderStatusCode = protocolResult.HttpStatusCode,
                ProviderLatencyMs = protocolResult.TransportLatencyMs,
                StartedAt = startedAt,
                CompletedAt = completedAt,
            });
    }

    private static IReadOnlyList<WorkerEvent> NormalizeEvents(string runId, string taskId, IReadOnlyList<WorkerEvent> events)
    {
        if (events.Count == 0)
        {
            return
            [
                new WorkerEvent
                {
                    RunId = runId,
                    TaskId = taskId,
                    EventType = WorkerEventType.FinalSummary,
                    Summary = "Remote API worker completed without emitting detailed events.",
                },
            ];
        }

        return events.Select(@event => new WorkerEvent
        {
            RunId = string.IsNullOrWhiteSpace(@event.RunId) ? runId : @event.RunId,
            TaskId = string.IsNullOrWhiteSpace(@event.TaskId) ? taskId : @event.TaskId,
            EventType = @event.EventType,
            Summary = @event.Summary,
            ItemType = @event.ItemType,
            CommandText = @event.CommandText,
            FilePath = @event.FilePath,
            ExitCode = @event.ExitCode,
            RawPayload = @event.RawPayload,
            Attributes = @event.Attributes,
            OccurredAt = @event.OccurredAt,
        }).ToArray();
    }

    private IReadOnlyList<CommandExecutionRecord> BuildCommandTrace(
        WorkerExecutionRequest request,
        ProviderProtocolResult protocolResult,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        var command = new[]
        {
            "remote-api",
            ProviderId,
            protocol.Metadata.RequestFamily ?? "request",
        };

        var exitCode = protocolResult.Status switch
        {
            WorkerExecutionStatus.Succeeded => 0,
            WorkerExecutionStatus.Skipped => 0,
            _ => 1,
        };

        var stdout = FirstNonEmpty(protocolResult.OutputText, protocolResult.Summary);
        var stderr = protocolResult.Status == WorkerExecutionStatus.Succeeded
            ? string.Empty
            : FirstNonEmpty(protocolResult.FailureReason, protocolResult.RawResponse);

        return
        [
            new CommandExecutionRecord(
                command,
                exitCode,
                stdout,
                stderr,
                Skipped: request.DryRun || protocolResult.Status == WorkerExecutionStatus.Skipped,
                WorkingDirectory: request.WorktreeRoot,
                Category: "remote_api",
                CapturedAt: completedAt == default ? startedAt : completedAt),
        ];
    }

    protected static string BuildPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length > 160 ? value[..160] : value;
    }

    protected static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    protected static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
