using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class OpenAiWorkerAdapter : RemoteApiWorkerAdapter
{
    public OpenAiWorkerAdapter(AiProviderConfig config, IHttpTransport transport, string selectionReason)
        : base(new OpenAiCompatibleProtocol(config), transport, selectionReason)
    {
    }

    public override string AdapterId => nameof(OpenAiWorkerAdapter);

    public override string BackendId => "openai_api";
}
