namespace Carves.Runtime.Domain.Platform;

public sealed class ActorArbitrationDecision
{
    public string ArbitrationId { get; init; } = $"arbitration-{Guid.NewGuid():N}";

    public OwnershipScope Scope { get; init; } = OwnershipScope.TaskMutation;

    public string TargetId { get; init; } = string.Empty;

    public string ChallengerActorSessionId { get; init; } = string.Empty;

    public ActorSessionKind ChallengerKind { get; init; } = ActorSessionKind.Operator;

    public string ChallengerIdentity { get; init; } = string.Empty;

    public ActorArbitrationOutcome Outcome { get; init; } = ActorArbitrationOutcome.DeniedChallenger;

    public string Summary { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string? CurrentOwnerActorSessionId { get; init; }

    public ActorSessionKind? CurrentOwnerKind { get; init; }

    public string? CurrentOwnerIdentity { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
