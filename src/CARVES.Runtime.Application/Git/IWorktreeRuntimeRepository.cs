using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Git;

public interface IWorktreeRuntimeRepository
{
    WorktreeRuntimeSnapshot Load();

    void Save(WorktreeRuntimeSnapshot snapshot);
}
