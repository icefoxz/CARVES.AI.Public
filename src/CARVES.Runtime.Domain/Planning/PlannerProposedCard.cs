namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposedCard
{
    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public string Priority { get; init; } = "P2";

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();
}
