using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IProviderHealthRepository
{
    ProviderHealthSnapshot Load();

    void Save(ProviderHealthSnapshot snapshot);
}
