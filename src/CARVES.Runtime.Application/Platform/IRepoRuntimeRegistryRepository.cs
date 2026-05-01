using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRepoRuntimeRegistryRepository
{
    RepoRuntimeRegistry Load();

    void Save(RepoRuntimeRegistry registry);
}
