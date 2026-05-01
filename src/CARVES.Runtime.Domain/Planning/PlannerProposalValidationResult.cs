namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposalValidationResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
