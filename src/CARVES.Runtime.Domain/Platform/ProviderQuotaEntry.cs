namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderQuotaEntry
{
    public string ProfileId { get; init; } = string.Empty;

    public int UsedThisHour { get; set; }

    public int LimitPerHour { get; init; }

    public DateTimeOffset WindowStartedAt { get; set; } = DateTimeOffset.UtcNow;

    public int Remaining => Math.Max(0, LimitPerHour - UsedThisHour);

    public bool Exhausted => UsedThisHour >= LimitPerHour;
}
