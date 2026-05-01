namespace Carves.Runtime.Domain.Platform;

public sealed class ResourceCleanupReport
{
    public string Trigger { get; init; } = string.Empty;

    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    public int RemovedWorktreeCount { get; init; }

    public int RemovedRecordCount { get; init; }

    public int RemovedRuntimeResidueCount { get; init; }

    public int RemovedEphemeralResidueCount { get; init; }

    public int PreservedActiveWorktreeCount { get; init; }

    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;

    public SustainabilityAuditReport? SustainabilityAudit { get; init; }
}
