using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    public ExecutionRun PrepareRunForDispatch(TaskNode task)
    {
        var activeRun = TryLoadActiveRun(task);
        if (activeRun is not null)
        {
            return activeRun.Status switch
            {
                ExecutionRunStatus.Planned => StartRun(activeRun),
                ExecutionRunStatus.Running => activeRun,
                _ => CreateAndStart(task, CreateInitialPlan(task)),
            };
        }

        return CreateAndStart(task, CreateInitialPlan(task));
    }

    public ExecutionRun EnsureRunForResult(TaskNode task, ResultEnvelope envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.ExecutionRunId)
            && TryLoad(envelope.ExecutionRunId!) is { } explicitRun)
        {
            return explicitRun.Status == ExecutionRunStatus.Planned ? StartRun(explicitRun) : explicitRun;
        }

        var activeRun = TryLoadActiveRun(task);
        if (activeRun is not null)
        {
            return activeRun.Status == ExecutionRunStatus.Planned ? StartRun(activeRun) : activeRun;
        }

        return CreateAndStart(task, CreateFallbackPlan(task));
    }

    public ExecutionRun CreateReplanRun(TaskNode task, ExecutionBoundaryReplanRequest replan, ExecutionRun? previousRun)
    {
        return CreateRun(task, CreateReplanPlan(task, replan, previousRun));
    }
}
