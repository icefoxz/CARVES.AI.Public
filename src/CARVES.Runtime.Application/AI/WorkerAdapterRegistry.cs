using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.AI;

public sealed class WorkerAdapterRegistry
{
    public WorkerAdapterRegistry(
        IReadOnlyList<IWorkerAdapter> adapters,
        IWorkerAdapter activeAdapter)
    {
        Adapters = adapters;
        ActiveAdapter = activeAdapter;
    }

    public IReadOnlyList<IWorkerAdapter> Adapters { get; }

    public IWorkerAdapter ActiveAdapter { get; }

    public IWorkerAdapter? TryGetByBackendId(string backendId)
    {
        return Adapters.FirstOrDefault(adapter => string.Equals(adapter.BackendId, backendId, StringComparison.OrdinalIgnoreCase));
    }

    public IWorkerAdapter Resolve(string backendId)
    {
        return TryGetByBackendId(backendId)
               ?? ActiveAdapter;
    }
}
