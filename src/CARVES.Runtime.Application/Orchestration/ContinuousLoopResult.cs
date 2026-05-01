using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Orchestration;

public sealed record ContinuousLoopResult
{
    public IReadOnlyList<CycleResult> Iterations { get; init; } = Array.Empty<CycleResult>();

    public RuntimeSessionState? Session { get; init; }

    public string Message { get; init; } = string.Empty;

    public int MaxIterations { get; init; }

    public int IterationsRun => Iterations.Count;

    public PlannerReentryResult? LastPlannerReentry => Iterations.LastOrDefault(iteration => iteration.PlannerReentry is not null)?.PlannerReentry;
}
