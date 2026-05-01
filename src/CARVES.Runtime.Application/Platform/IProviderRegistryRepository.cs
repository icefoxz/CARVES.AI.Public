using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IProviderRegistryRepository
{
    ProviderRegistry Load();

    void Save(ProviderRegistry registry);
}
