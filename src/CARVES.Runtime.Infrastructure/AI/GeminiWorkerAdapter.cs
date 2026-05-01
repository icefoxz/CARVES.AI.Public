using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class GeminiWorkerAdapter : RemoteApiWorkerAdapter
{
    public GeminiWorkerAdapter(AiProviderConfig config, IHttpTransport transport, string selectionReason)
        : base(new GeminiProviderProtocol(config), transport, selectionReason)
    {
    }

    public override string AdapterId => nameof(GeminiWorkerAdapter);

    public override string BackendId => "gemini_api";
}
