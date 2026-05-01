using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IOwnershipRepository
{
    OwnershipSnapshot Load();

    void Save(OwnershipSnapshot snapshot);
}
