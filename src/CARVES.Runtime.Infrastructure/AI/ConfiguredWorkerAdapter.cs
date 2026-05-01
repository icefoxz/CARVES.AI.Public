using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.Infrastructure.AI;

internal abstract class ConfiguredWorkerAdapter : IWorkerAdapter
{
    private readonly IAiClient client;

    protected ConfiguredWorkerAdapter(IAiClient client, string selectionReason)
    {
        this.client = client;
        SelectionReason = selectionReason;
    }

    public abstract string AdapterId { get; }

    public virtual string BackendId => ProviderId;

    public abstract string ProviderId { get; }

    public bool IsConfigured => client.IsConfigured;

    public bool IsRealAdapter => true;

    public string SelectionReason { get; }

    protected virtual WorkerProviderCapabilities Capabilities => new()
    {
        SupportsExecution = true,
        SupportsEventStream = true,
        SupportsHealthProbe = true,
        SupportsCancellation = false,
        SupportsTrustedProfiles = false,
        SupportsNetworkAccess = false,
        SupportsDotNetBuild = true,
        SupportsLongRunningTasks = false,
    };

    public WorkerProviderCapabilities GetCapabilities()
    {
        return Capabilities;
    }

    public virtual WorkerBackendHealthSummary CheckHealth()
    {
        return new WorkerBackendHealthSummary
        {
            State = IsConfigured ? WorkerBackendHealthState.Healthy : WorkerBackendHealthState.Unavailable,
            Summary = IsConfigured
                ? $"{ProviderId} worker adapter is configured."
                : $"{ProviderId} worker adapter is unavailable because the AI client is not configured.",
        };
    }

    public virtual WorkerRunControlResult Cancel(string runId, string reason)
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
        var record = client.Execute(new AiExecutionRequest(
            request.TaskId,
            request.Title,
            request.Instructions,
            request.Input,
            request.MaxOutputTokens,
            request.ModelOverride));

        return new WorkerExecutionResult
        {
            TaskId = request.TaskId,
            BackendId = BackendId,
            ProviderId = ProviderId,
            AdapterId = AdapterId,
            AdapterReason = SelectionReason,
            ProfileId = request.Profile.ProfileId,
            TrustedProfile = request.Profile.Trusted,
            Status = record.Succeeded ? WorkerExecutionStatus.Succeeded : WorkerExecutionStatus.Failed,
            FailureKind = record.Succeeded ? WorkerFailureKind.None : WorkerFailureKind.Unknown,
            Retryable = false,
            Configured = record.Configured,
            Model = record.Model,
            RequestId = record.RequestId,
            RequestPreview = record.RequestPreview,
            RequestHash = record.RequestHash,
            ResponsePreview = record.ResponsePreview,
            ResponseHash = record.ResponseHash,
            Summary = record.OutputText ?? record.ResponsePreview ?? record.FailureReason ?? SelectionReason,
            Rationale = record.OutputText,
            FailureReason = record.FailureReason,
            InputTokens = record.InputTokens,
            OutputTokens = record.OutputTokens,
            StartedAt = record.CapturedAt,
            CompletedAt = record.CapturedAt,
            Events =
            [
                new WorkerEvent
                {
                    RunId = record.RequestId ?? $"worker-run-{Guid.NewGuid():N}",
                    TaskId = request.TaskId,
                    EventType = WorkerEventType.FinalSummary,
                    Summary = record.OutputText ?? record.FailureReason ?? SelectionReason,
                    RawPayload = record.OutputText,
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["provider"] = ProviderId,
                        ["model"] = record.Model,
                    },
                },
            ],
        };
    }
}
