using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.AI;

public interface IAiClient
{
    string ClientName { get; }

    bool IsConfigured { get; }

    AiExecutionRecord Execute(AiExecutionRequest request);
}
