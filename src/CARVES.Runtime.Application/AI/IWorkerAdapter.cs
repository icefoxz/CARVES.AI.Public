using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.AI;

public interface IWorkerAdapter
{
    string AdapterId { get; }

    string BackendId { get; }

    string ProviderId { get; }

    bool IsConfigured { get; }

    bool IsRealAdapter { get; }

    string SelectionReason { get; }

    WorkerProviderCapabilities GetCapabilities();

    WorkerBackendHealthSummary CheckHealth();

    WorkerRunControlResult Cancel(string runId, string reason);

    WorkerExecutionResult Execute(WorkerExecutionRequest request);
}
