namespace Carves.Runtime.Application.Interaction;

public interface IIntentDraftRepository
{
    IntentDiscoveryDraft? Load();

    void Save(IntentDiscoveryDraft draft);

    IntentDiscoveryDraft Update(Func<IntentDiscoveryDraft, IntentDiscoveryDraft> update);

    void Delete();
}
