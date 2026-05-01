using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal abstract class DisabledWorkerAdapter : IWorkerAdapter
{
    protected DisabledWorkerAdapter(string selectionReason)
    {
        SelectionReason = selectionReason;
    }

    public abstract string AdapterId { get; }

    public virtual string BackendId => ProviderId;

    public abstract string ProviderId { get; }

    public bool IsConfigured => false;

    public bool IsRealAdapter => false;

    public string SelectionReason { get; }

    protected virtual WorkerProviderCapabilities Capabilities => new()
    {
        SupportsExecution = true,
        SupportsEventStream = false,
        SupportsHealthProbe = true,
        SupportsCancellation = false,
        SupportsTrustedProfiles = false,
        SupportsNetworkAccess = false,
        SupportsDotNetBuild = false,
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
            State = WorkerBackendHealthState.Disabled,
            Summary = SelectionReason,
        };
    }

    public WorkerRunControlResult Cancel(string runId, string reason)
    {
        return new WorkerRunControlResult
        {
            BackendId = BackendId,
            RunId = runId,
            Supported = false,
            Succeeded = false,
            Summary = $"{AdapterId} is disabled and cannot cancel runs.",
        };
    }

    public WorkerExecutionResult Execute(WorkerExecutionRequest request)
    {
        var preview = request.Input.Length > 160 ? request.Input[..160] : request.Input;
        return WorkerExecutionResult.Skipped(
            request.TaskId,
            BackendId,
            ProviderId,
            AdapterId,
            request.Profile,
            SelectionReason,
            preview,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant()) with
        {
            Model = request.ModelOverride ?? "disabled",
            Events =
            [
                new WorkerEvent
                {
                    TaskId = request.TaskId,
                    EventType = WorkerEventType.FinalSummary,
                    Summary = SelectionReason,
                    RawPayload = SelectionReason,
                },
            ],
        };
    }
}
