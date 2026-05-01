using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Planning;

public interface IManagedWorkspaceLeaseRepository
{
    ManagedWorkspaceLeaseSnapshot Load();

    void Save(ManagedWorkspaceLeaseSnapshot snapshot);
}
