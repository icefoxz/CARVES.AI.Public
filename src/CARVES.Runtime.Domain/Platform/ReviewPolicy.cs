namespace Carves.Runtime.Domain.Platform;

public sealed class ReviewPolicy
{
    public string PolicyId { get; init; } = string.Empty;

    public bool ManualOnArchitectureChange { get; init; }

    public bool ManualOnAutonomousRefactor { get; init; }

    public bool ManualOnProviderRotation { get; init; }
}
