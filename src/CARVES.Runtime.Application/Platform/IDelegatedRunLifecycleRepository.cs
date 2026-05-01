using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IDelegatedRunLifecycleRepository
{
    DelegatedRunLifecycleSnapshot Load();

    void Save(DelegatedRunLifecycleSnapshot snapshot);
}
