namespace Carves.Runtime.Domain.Platform;

public enum ProjectionFreshness
{
    Fresh,
    Stale,
    Diverged,
    Unavailable,
}
