using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IProviderQuotaRepository
{
    ProviderQuotaSnapshot Load();

    void Save(ProviderQuotaSnapshot snapshot);
}
