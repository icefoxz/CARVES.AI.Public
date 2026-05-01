namespace Carves.Runtime.Application.Platform;

public sealed record ProviderQuotaSummaryDto(
    string ProfileId,
    int UsedThisHour,
    int LimitPerHour,
    int Remaining,
    bool Exhausted);
