using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IWorkerLeaseRepository
{
    IReadOnlyList<WorkerLeaseRecord> Load();

    void Save(IReadOnlyList<WorkerLeaseRecord> leases);
}
