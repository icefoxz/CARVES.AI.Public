namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposedDependency
{
    public string FromTaskId { get; init; } = string.Empty;

    public string ToTaskId { get; init; } = string.Empty;
}
