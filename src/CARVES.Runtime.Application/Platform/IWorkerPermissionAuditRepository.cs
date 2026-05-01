using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform;

public interface IWorkerPermissionAuditRepository
{
    IReadOnlyList<WorkerPermissionAuditRecord> Load();

    void Save(IReadOnlyList<WorkerPermissionAuditRecord> records);
}
