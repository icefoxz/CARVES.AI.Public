using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class ClaudeWorkerAdapter : RemoteApiWorkerAdapter
{
    public ClaudeWorkerAdapter(AiProviderConfig config, IHttpTransport transport, string selectionReason)
        : base(new ClaudeProviderProtocol(config), transport, selectionReason)
    {
    }

    public override string AdapterId => nameof(ClaudeWorkerAdapter);

    public override string BackendId => "claude_api";
}
