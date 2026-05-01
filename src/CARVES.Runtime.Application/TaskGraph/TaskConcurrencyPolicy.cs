using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Application.TaskGraph;

public sealed class TaskConcurrencyPolicy
{
    public const int Phase1DispatchCap = 2;

    public int ResolveDispatchCapacity(WorkerPoolSnapshot workerPool)
    {
        return Math.Max(0, Math.Min(Phase1DispatchCap, workerPool.MaxWorkers - workerPool.ActiveWorkers));
    }
}
