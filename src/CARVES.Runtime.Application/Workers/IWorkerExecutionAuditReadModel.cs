namespace Carves.Runtime.Application.Workers;

public interface IWorkerExecutionAuditReadModel
{
    string StoragePath { get; }

    bool StorageExists { get; }

    void AppendExecution(WorkerExecutionAuditEntry entry);

    IReadOnlyList<WorkerExecutionAuditEntry> QueryRecent(int limit);

    WorkerExecutionAuditSummary GetSummary();
}

public interface IWorkerExecutionAuditQueryReadModel : IWorkerExecutionAuditReadModel
{
    WorkerExecutionAuditQueryResult Query(WorkerExecutionAuditQuery query);
}
