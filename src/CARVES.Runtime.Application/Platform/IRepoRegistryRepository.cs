using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRepoRegistryRepository
{
    RepoRegistry Load();

    void Save(RepoRegistry registry);
}
