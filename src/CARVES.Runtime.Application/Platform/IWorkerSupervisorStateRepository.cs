using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IWorkerSupervisorStateRepository
{
    WorkerSupervisorStateSnapshot Load();

    void Save(WorkerSupervisorStateSnapshot snapshot);
}
