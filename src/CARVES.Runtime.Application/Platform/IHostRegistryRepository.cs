using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IHostRegistryRepository
{
    HostRegistry Load();

    void Save(HostRegistry registry);
}
