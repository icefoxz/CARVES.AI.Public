using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposedTask
{
    public string TempId { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public TaskType TaskType { get; init; } = TaskType.Planning;

    public string Priority { get; init; } = "P2";

    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public string ProposalSource { get; init; } = string.Empty;

    public string ProposalReason { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public RealityProofTarget? ProofTarget { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
