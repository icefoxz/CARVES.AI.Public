using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.AI;

public interface IProviderProtocol
{
    string ProviderId { get; }

    ProviderProtocolMetadata Metadata { get; }

    bool IsConfigured { get; }

    WorkerBackendHealthSummary CheckHealth();

    HttpTransportRequest BuildRequest(WorkerExecutionRequest request);

    ProviderProtocolResult ParseResponse(WorkerExecutionRequest request, HttpTransportResponse response);

    ProviderProtocolResult FromException(WorkerExecutionRequest request, Exception exception);
}
