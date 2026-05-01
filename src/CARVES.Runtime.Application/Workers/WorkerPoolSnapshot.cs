namespace Carves.Runtime.Application.Workers;

public sealed record WorkerPoolSnapshot(int ActiveWorkers, int MaxWorkers, IReadOnlyList<string> ActiveTaskIds)
{
    public bool HasCapacity => ActiveWorkers < MaxWorkers;

    public int AvailableWorkers => Math.Max(0, MaxWorkers - ActiveWorkers);
}
