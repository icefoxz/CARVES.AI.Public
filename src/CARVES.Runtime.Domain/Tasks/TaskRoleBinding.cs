namespace Carves.Runtime.Domain.Tasks;

public sealed class TaskRoleBinding
{
    public string Producer { get; init; } = string.Empty;

    public string Executor { get; init; } = string.Empty;

    public string Reviewer { get; init; } = string.Empty;

    public string Approver { get; init; } = string.Empty;

    public string ScopeSteward { get; init; } = string.Empty;

    public string PolicyOwner { get; init; } = string.Empty;
}
