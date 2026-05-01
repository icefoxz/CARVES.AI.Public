namespace Carves.Runtime.Application.Planning;

public sealed class PlannerAdapterRegistry
{
    public PlannerAdapterRegistry(IReadOnlyList<IPlannerAdapter> adapters, IPlannerAdapter activeAdapter)
    {
        Adapters = adapters;
        ActiveAdapter = activeAdapter;
    }

    public IReadOnlyList<IPlannerAdapter> Adapters { get; }

    public IPlannerAdapter ActiveAdapter { get; }
}
