namespace Carves.Runtime.Domain.Platform;

public sealed class OwnershipDecision
{
    public bool Allowed { get; init; }

    public OwnershipDecisionOutcome Outcome { get; init; } = OwnershipDecisionOutcome.Denied;

    public string Summary { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public OwnershipBinding? Binding { get; init; }

    public string? ExistingOwnerActorSessionId { get; init; }

    public ActorSessionKind? ExistingOwnerKind { get; init; }

    public string? ExistingOwnerIdentity { get; init; }
}
