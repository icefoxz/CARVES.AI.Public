namespace Carves.Runtime.Domain.Platform;

public sealed class DelegatedRunLifecycleSnapshot
{
    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<DelegatedRunLifecycleRecord> Records { get; init; } = Array.Empty<DelegatedRunLifecycleRecord>();
}
