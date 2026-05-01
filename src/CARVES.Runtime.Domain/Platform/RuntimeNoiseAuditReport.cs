namespace Carves.Runtime.Domain.Platform;

public sealed class RuntimeNoiseAuditTaskEntry
{
    public string TaskId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public RuntimeNoiseAuditClassification Classification { get; init; } = RuntimeNoiseAuditClassification.ProjectionNoise;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeNoiseAuditIncidentEntry
{
    public string IncidentId { get; init; } = string.Empty;

    public string IncidentType { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public RuntimeNoiseAuditClassification Classification { get; init; } = RuntimeNoiseAuditClassification.ProjectionNoise;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeNoiseAuditReport
{
    public RuntimeNoiseStartGateVerdict StartGate { get; init; } = RuntimeNoiseStartGateVerdict.Clear;

    public IReadOnlyList<RuntimeNoiseAuditTaskEntry> BlockedTasks { get; init; } = Array.Empty<RuntimeNoiseAuditTaskEntry>();

    public IReadOnlyList<RuntimeNoiseAuditIncidentEntry> Incidents { get; init; } = Array.Empty<RuntimeNoiseAuditIncidentEntry>();

    public IReadOnlyDictionary<string, int> ClassificationCounts { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);
}
