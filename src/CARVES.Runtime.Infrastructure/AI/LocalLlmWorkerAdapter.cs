namespace Carves.Runtime.Infrastructure.AI;

internal sealed class LocalLlmWorkerAdapter : DisabledWorkerAdapter
{
    public LocalLlmWorkerAdapter(string selectionReason)
        : base(selectionReason)
    {
    }

    public override string AdapterId => nameof(LocalLlmWorkerAdapter);

    public override string BackendId => "local_agent";

    public override string ProviderId => "local";
}
