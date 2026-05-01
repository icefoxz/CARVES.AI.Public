namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderHealthSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<ProviderHealthRecord> Entries { get; init; } = Array.Empty<ProviderHealthRecord>();
}
