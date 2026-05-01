using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IWorkerNodeRegistryRepository
{
    IReadOnlyList<WorkerNode> Load();

    void Save(IReadOnlyList<WorkerNode> nodes);
}
