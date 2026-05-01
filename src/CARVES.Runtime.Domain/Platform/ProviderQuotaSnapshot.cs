namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderQuotaSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<ProviderQuotaEntry> Entries { get; init; } = Array.Empty<ProviderQuotaEntry>();
}
