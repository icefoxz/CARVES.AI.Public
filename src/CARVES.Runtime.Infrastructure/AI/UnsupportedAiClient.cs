using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class UnsupportedAiClient : IAiClient
{
    private readonly string providerId;

    public UnsupportedAiClient(string providerId)
    {
        this.providerId = providerId;
    }

    public string ClientName => $"{providerId}-unsupported";

    public bool IsConfigured => false;

    public AiExecutionRecord Execute(AiExecutionRequest request)
    {
        throw new InvalidOperationException($"AI provider '{providerId}' is not implemented by the current runtime.");
    }
}
