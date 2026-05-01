using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.TaskGraph;

public interface ITaskGraphRepository
{
    DomainTaskGraph Load();

    void Save(DomainTaskGraph graph);

    void Upsert(TaskNode task);

    void UpsertRange(IEnumerable<TaskNode> tasks);

    T WithWriteLock<T>(Func<T> action);
}
