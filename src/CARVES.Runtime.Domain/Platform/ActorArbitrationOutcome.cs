namespace Carves.Runtime.Domain.Platform;

public enum ActorArbitrationOutcome
{
    Granted = 0,
    DeniedChallenger = 1,
    Deferred = 2,
    Escalated = 3,
}
