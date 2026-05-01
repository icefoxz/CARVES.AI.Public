namespace Carves.Runtime.Domain.Platform;

public sealed class HostRestartRehydrationReport
{
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    public int InvalidatedLeaseCount { get; init; }

    public int ReconciledTaskCount { get; init; }

    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}
