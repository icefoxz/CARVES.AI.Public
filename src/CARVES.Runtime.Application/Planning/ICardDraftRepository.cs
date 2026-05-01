using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public interface ICardDraftRepository
{
    IReadOnlyList<CardDraftRecord> List();

    CardDraftRecord? TryGet(string draftIdOrCardId);

    void Save(CardDraftRecord record);
}
