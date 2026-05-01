using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerHostLoopResult
{
    public IReadOnlyList<PlannerHostResult> Iterations { get; init; } = Array.Empty<PlannerHostResult>();

    public RuntimeSessionState? Session { get; init; }

    public int IterationsRun => Iterations.Count;

    public int MaxIterations { get; init; }

    public string Message { get; init; } = string.Empty;
}
