namespace Carves.Runtime.Domain.Safety;

public sealed class SafetyValidatorResult
{
    public string ValidatorName { get; init; } = string.Empty;

    public SafetyOutcome Outcome { get; init; } = SafetyOutcome.Allow;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<SafetyViolation> Violations { get; init; } = Array.Empty<SafetyViolation>();
}
