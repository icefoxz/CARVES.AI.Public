namespace Carves.Runtime.Domain.Tasks;

public sealed class ValidationPlan
{
    public IReadOnlyList<IReadOnlyList<string>> Commands { get; init; } = Array.Empty<IReadOnlyList<string>>();

    public IReadOnlyList<string> Checks { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExpectedEvidence { get; init; } = Array.Empty<string>();
}
