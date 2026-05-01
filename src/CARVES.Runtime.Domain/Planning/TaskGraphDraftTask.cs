using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Domain.Planning;

public sealed class TaskGraphDraftTask
{
    public string TaskId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public TaskType TaskType { get; init; } = TaskType.Execution;

    public string Priority { get; init; } = "P1";

    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public string? AcceptanceContractProjectionSource { get; init; }

    public string? AcceptanceContractProjectionPolicy { get; init; }

    public string? AcceptanceContractProjectionReason { get; init; }

    public RealityProofTarget? ProofTarget { get; init; }

    public TaskRoleBinding? RoleBinding { get; init; }
}
