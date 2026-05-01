using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRuntimeInstanceRepository
{
    IReadOnlyList<RuntimeInstance> Load();

    void Save(IReadOnlyList<RuntimeInstance> instances);
}
